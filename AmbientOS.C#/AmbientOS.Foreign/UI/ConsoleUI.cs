using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AmbientOS.Environment;
using AmbientOS.Utils;

namespace AmbientOS.UI
{
    public class ConsoleUI : IUIImpl
    {
        public IUI UIRef { get; }

        public LogContext LogContext { get; }

        private readonly IConsole console;
        private readonly ProducerConsumerQueue<Tuple<string, ConsoleColor, ConsoleColor>> queue = new ProducerConsumerQueue<Tuple<string, ConsoleColor, ConsoleColor>>();
        private readonly Stack<Dialog> dialogs = new Stack<Dialog>();
        private readonly AutoResetEvent updateDialogs = new AutoResetEvent(false);
        private readonly ManualResetEvent stackEmpty = new ManualResetEvent(true);


        public ConsoleUI(IConsole console)
        {
            UIRef = new UIRef(this);
            this.console = console;
            LogContext = LogContext.FromConsole((str, foregroundColor, backgroundColor, controller) => {
                queue.Enqueue(new Tuple<string, ConsoleColor, ConsoleColor>(str, foregroundColor, backgroundColor), controller);
            }, "root");
        }

        public void Start(TaskController controller)
        {
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

            // logging thread
            new Thread(() => {
                Tuple<string, ConsoleColor, ConsoleColor> item;
                while (queue.TryDequeue(out item, controller)) {
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
            new Thread(() => {
                while (true) {
                    controller.WaitOne(updateDialogs);
                    Dialog dialog = topDialog();
                    if (dialog != null) {
                        lock (dialog) {
                            Draw(dialog);
                        }
                    } else {
                        console.Clear(ConsoleColor.DefaultBackground);
                    }
                }
            }).Start();

            // input thread
            new Thread(() => {
                while (true) {
                    var keyPress = console.Read();
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
            public long SelectedOption { get; set; }
            public ProducerConsumerQueue<KeyPress> KeyPresses { get; set; }
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
                console.SetCursorPosition(new Vector2D<int>(0, i), false);
                console.Write(lines[i].Item1, lines[i].Item2 ? ConsoleColor.DefaultBackground : ConsoleColor.DefaultForeground, lines[i].Item2 ? ConsoleColor.DefaultForeground : ConsoleColor.DefaultBackground);
            }
        }

        public long PresentDialog(Text message, Option[] options)
        {
            var dialog = new Dialog() {
                Valid = true,
                Message = message,
                Options = options,
                DetailsExpanded = false, // todo: set according to user preference
                SelectedOption = 0,
                Offset = 0,
                BufferSize = console.GetDimensions(),
                KeyPresses = new ProducerConsumerQueue<KeyPress>()
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

                KeyPress keyPress;
                while (dialog.KeyPresses.TryDequeue(out keyPress, new TaskController())) { // todo: use real task controller
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
