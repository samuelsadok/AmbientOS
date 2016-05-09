using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AmbientOS.Environment;

namespace AmbientOS.UI
{
    public class ConsoleUI : IShellImpl
    {
        public IShell ShellRef { get; }

        public LogContext LogContext { get; }

        private readonly IConsole console;
        private readonly DynamicQueue<Tuple<string, ConsoleColor, ConsoleColor>> queue = new DynamicQueue<Tuple<string, ConsoleColor, ConsoleColor>>();
        private readonly Stack<Dialog> dialogs = new Stack<Dialog>();
        private readonly AutoResetEvent updateDialogs = new AutoResetEvent(false);
        private readonly ManualResetEvent stackEmpty = new ManualResetEvent(true);

        private bool inGraphicMode;


        public ConsoleUI(IConsole console)
        {
            ShellRef = new ShellRef(this);
            this.console = console;
            LogContext = LogContext.FromConsole((str, foregroundColor, backgroundColor, controller) => {
                queue.Enqueue(new Tuple<string, ConsoleColor, ConsoleColor>(str, foregroundColor, backgroundColor), controller);
            }, "root");
        }

        public void Start(Context context)
        {
            TaskController controller = context.Controller;

            Func<Dialog> topDialog = () => {
                lock (dialogs) {
                    while (dialogs.Any()) {
                        var dialog = dialogs.Peek();
                        if (dialog.Valid)
                            return dialog;
                        dialogs.Pop();
                    }
                    stackEmpty.Set();
                    return null;
                }
            };


            Action newPage = () => {
                var windowSize = console.WindowSize;
                var pos = console.CursorPosition.GetValue();
                var size = windowSize.GetValue();
                console.Write(new string(' ', (size.X - pos.X) + (size.Y - pos.Y) * size.X));
            };

            Action reversePage = () => {
                var windowSize = console.WindowSize;
                console.Clear();
                console.Scroll(-windowSize.GetValue().Y);
            };

            //Action printAbove = () => {
            //    console.CopyArea(new Vector2D<int>(0, 0), new Vector2D<int>(0, 1), console.WindowSize.GetValue() - new Vector2D<int>(0, 1));
            //    console.SetCursorPosition(new Vector2D<int>(0, 0), true);
            //};

            // logging thread
            new CancelableThread(() => {
                while (true) {
                    var item = queue.Dequeue(controller);
                    while (true) {
                        controller.WaitOne(stackEmpty);
                        lock (dialogs) {
                            if (!dialogs.Any()) {
                                console.Write(item.Item1, item.Item2, item.Item3);
                                break;
                            }
                        }
                    }
                }
            }).Start();

            // draw dialog thread
            new CancelableThread(() => {
                while (true) {
                    controller.WaitOne(updateDialogs);
                    Dialog dialog = topDialog();
                    if (dialog != null) {
                        lock (dialog) {
                            if (!inGraphicMode)
                                newPage();
                            inGraphicMode = true;
                            Draw(dialog);
                        }
                    } else {
                        console.Clear(ConsoleColor.DefaultBackground);
                    }
                }
            }).Start();

            // input thread
            new CancelableThread(() => {
                while (true) {
                    var keyPress = console.Read(context);
                    controller.ThrowIfCancellationRequested();
                    Dialog dialog = topDialog();
                    if (dialog != null)
                        dialog.KeyPresses.Enqueue(keyPress, controller);
                }
            }).Start();
        }

        class Dialog
        {
            public bool Valid { get; set; }
            public Text Message { get; set; }
            public Option[] Options { get; set; }
            public Vector2D<int> BufferSize { get; set; }
            public int Offset { get; set; }
            public bool DetailsExpanded { get; set; }
            public int SelectedOption { get; set; }
            public DynamicQueue<KeyPress> KeyPresses { get; set; }
        }

        private IEnumerable<Tuple<string, bool>> ToLines(Dialog dialog)
        {
            foreach (var line in console.ToLines(dialog.Message.Summary))
                yield return new Tuple<string, bool>(line, false);

            yield return new Tuple<string, bool>("", false);
            yield return new Tuple<string, bool>(string.Format(" Details [SPACE to {1}] {0}", dialog.DetailsExpanded ? "^" : "v", dialog.DetailsExpanded ? "hide" : "show"), false);

            if (dialog.DetailsExpanded)
                foreach (var line in console.ToLines(dialog.Message.Details, "   "))
                    yield return new Tuple<string, bool>(line, false);
            yield return new Tuple<string, bool>("", false);

            for (int i = 0; i < dialog.Options.Count(); i++)
                foreach (var line in console.ToLines(dialog.Options[i].Text.Summary, "  "))
                    yield return new Tuple<string, bool>(line, i == dialog.SelectedOption);
        }

        private void Draw(Dialog dialog)
        {
            console.Clear(ConsoleColor.DefaultBackground);
            var lines = ToLines(dialog).Skip(dialog.Offset).Take(dialog.BufferSize.Y).ToArray();
            for (int i = 0; i < lines.Count(); i++) {
                console.CursorPosition.SetValue(new Vector2D<int>(0, i));
                console.Write(lines[i].Item1, lines[i].Item2 ? ConsoleColor.DefaultBackground : ConsoleColor.DefaultForeground, lines[i].Item2 ? ConsoleColor.DefaultForeground : ConsoleColor.DefaultBackground);
            }
        }

        public int PresentDialog(Text message, Option[] options)
        {
            var dialog = new Dialog() {
                Valid = true,
                Message = message,
                Options = options,
                DetailsExpanded = false, // todo: set according to user preference
                SelectedOption = 0,
                Offset = 0,
                BufferSize = console.WindowSize.GetValue(),
                KeyPresses = new DynamicQueue<KeyPress>()
            };

            var escapeOption = -1;

            for (int i = options.Count() - 1; i >= 0; i--)
                if (options[i].Level == Level.Recommended)
                    dialog.SelectedOption = i;
                else if (options[i].Level == Level.Escape)
                    escapeOption = i;

            lock (dialogs) {
                dialogs.Push(dialog);
                updateDialogs.Set();
            }

            try {
                while (true) { // todo: use real task controller
                    var keyPress = dialog.KeyPresses.Dequeue(new TaskController());

                    if (keyPress.Key == Key.Enter)
                        break;

                    lock (dialog) {
                        switch (keyPress.Key) {
                            case Key.ArrowUp:
                                dialog.SelectedOption = (dialog.SelectedOption <= 0 ? options.Count() : dialog.SelectedOption) - 1;
                                break;
                            case Key.ArrowDown:
                                dialog.SelectedOption = (dialog.SelectedOption + 1) % options.Count();
                                break;
                            case Key.Space:
                                dialog.DetailsExpanded ^= true;
                                break;
                            case Key.Esc:
                                if (escapeOption < 0)
                                    continue;
                                return escapeOption;
                            default:
                                continue;
                        }
                    }

                    updateDialogs.Set();
                }

                return dialog.SelectedOption;
            } finally {
                lock (dialog) {
                    dialog.Valid = false;
                }
                updateDialogs.Set();
            }
        }

        /*public long PresentDialog(Text message, Option[] options)
        {
            console.WriteLine(message.Summary, ConsoleColor.Green);
            console.WriteLine(message.Details, ConsoleColor.DarkGray);
            var indexedOptions = options.Select((o, i) => new { opt = o, index = i }).ToArray();
            var table = Utilities.CreateStringTable(indexedOptions,
                o => string.Format(o.opt.Level == Level.Recommended ? "[{0}]" : " {0}", o.index + 1),
                o => o.opt.Text.Summary,
                o => o.opt.Text.Details);

            for (int row = 0; row < options.Count(); row++) {
                console.Write("   " + table[row, 0] + " " + table[row, 1], ConsoleColor.Green);
                console.WriteLine(" " + table[row, 2], ConsoleColor.DarkGray);
            }

            var defaultOption = indexedOptions.FirstOrDefault(o => o.opt.Level == Level.Recommended);

            while (true) {
                console.Write("select an input (1 - " + options.Count() + ")" + (defaultOption == null ? "" : " or press enter: "), ConsoleColor.Gray);
                var val = console.ReadNumber(defaultOption?.index);
                if (val.HasValue)
                    if (val >= 1 && val <= options.Count())
                        return val.Value - 1;
                console.WriteLine("invalid input", ConsoleColor.Red);
            }
        }*/

        private LogType ToLogType(Severity severity)
        {
            switch (severity) { // todo: unify LogType & Severity
                case Severity.Error: return LogType.Error;
                case Severity.Info: return LogType.Info;
                case Severity.Success: return LogType.Success;
                case Severity.Warning: return LogType.Warning;
                default: return LogType.Debug;
            }
        }


        public void Notify(Text message, Severity severity)
        {
            LogContext.Log(message.Summary, ToLogType(severity));
            if (message.Details != null)
                LogContext.Log("Details: " + message.Details, ToLogType(severity));
        }
    }
}
