using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.UI
{
    /// <summary>
    /// A messy way to provide a simple way of generating custom templates in code.
    /// Don't use this class directly, use Abstraction.GetTemplate instead.
    /// </summary>
    public class ControlTemplate<T>
        : System.Windows.Controls.ContentControl
    {
        private T Data { get; set; }
        private Func<T, System.Windows.FrameworkElement> SetupAction { get; set; }
        private Func<T, object> TagSetter { get; set; }

        private bool dataSet, setupActionSet, tagSetterSet;

        private void Setup()
        {
            if (dataSet) {
                if (setupActionSet)
                    Content = SetupAction(Data);
                if (tagSetterSet)
                    Tag = TagSetter(Data);
            }
        }

        public static System.Windows.DependencyProperty DataProperty = System.Windows.DependencyProperty.Register("Data",
                typeof(T),
                typeof(ControlTemplate<T>),
                new System.Windows.PropertyMetadata((o, e) => {
                    var control = (ControlTemplate<T>)(object)o;
                    control.Data = (T)e.NewValue;
                    control.dataSet = true;
                    control.Setup();
                }));

        public static System.Windows.DependencyProperty SetupActionProperty = System.Windows.DependencyProperty.Register("SetupAction", typeof(Func<T, System.Windows.FrameworkElement>), typeof(ControlTemplate<T>),
                new System.Windows.PropertyMetadata((o, e) => {
                    var control = (ControlTemplate<T>)(object)o;
                    control.SetupAction = (Func<T, System.Windows.FrameworkElement>)e.NewValue;
                    control.setupActionSet = true;
                    control.Setup();
                }));

        public static System.Windows.DependencyProperty TagSetterProperty = System.Windows.DependencyProperty.Register("TagSetter", typeof(Func<T, object>), typeof(ControlTemplate<T>),
                new System.Windows.PropertyMetadata((o, e) => {
                    var control = (ControlTemplate<T>)(object)o;
                    control.TagSetter = (Func<T, object>)e.NewValue;
                    control.tagSetterSet = true;
                    control.Setup();
                }));
    }


    public class HierarchicalTemplateSelector<TTree, TItem>
        : System.Windows.Controls.DataTemplateSelector
    {
        private System.Windows.DataTemplate folderTemplate;
        private System.Windows.DataTemplate itemTemplate;

        public HierarchicalTemplateSelector(Func<TreeSource<TTree, TItem>, View> folderConstructor, Func<TItem, View> itemConstructor)
        {
            folderTemplate = Abstraction.GetHierarchicalTemplate(folderConstructor, this);
            itemTemplate = Abstraction.GetTemplate(itemConstructor);
        }

        public override System.Windows.DataTemplate SelectTemplate(object item, System.Windows.DependencyObject container)
        {
            Application.UILog.Log("select type: " + item);
            var tree = item as TreeSource<TTree, TItem>;
            if (tree == null)
                return itemTemplate;
            else
                return folderTemplate;
        }
    }


    public static partial class Abstraction
    {

        public static System.Windows.FrameworkElementFactory GetElementFactory<T>(Func<T, System.Windows.FrameworkElement> itemConstructor, Func<T, object> tagSetter = null)
        {
            if (itemConstructor == null) throw new ArgumentNullException();

            System.Windows.FrameworkElementFactory factory = new System.Windows.FrameworkElementFactory(typeof(ControlTemplate<T>));
            factory.SetValue(ControlTemplate<T>.SetupActionProperty, itemConstructor);
            factory.SetValue(ControlTemplate<T>.TagSetterProperty, tagSetter);
            factory.SetBinding(ControlTemplate<T>.DataProperty, new Binding(""));
            return factory;
        }

        /// <summary>
        /// Generates a DataTemplate that uses the provided function to generate a framework element for a specific data instance.
        /// </summary>
        public static System.Windows.DataTemplate GetTemplate<T>(Func<T, View> itemConstructor, Func<T, object> tagSetter = null)
        {
            return new System.Windows.DataTemplate(typeof(ControlTemplate<T>)) {
                VisualTree = GetElementFactory((obj) => itemConstructor(obj).NativeView, tagSetter)
            };
        }

        /// <summary>
        /// Generates a ControlTemplate that uses the provided function to generate a framework element for a specific data instance.
        /// </summary>
        public static System.Windows.Controls.ControlTemplate GetControlTemplate<T>()
        {
            //return new System.Windows.Controls.ControlTemplate(typeof(T)) {
            //    VisualTree = GetElementFactory((obj) => itemConstructor(obj).NativeView, tagSetter)
            //};
            return new System.Windows.Controls.ControlTemplate(typeof(T)) {
                VisualTree = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter))
            };
        }

        /// <summary>
        /// Generates a DataTemplate that uses the provided function to generate a framework element for a specific data instance.
        /// </summary>
        public static System.Windows.DataTemplate GetHierarchicalTemplate<TTree, TItem>(Func<TreeSource<TTree, TItem>, View> folderConstructor, HierarchicalTemplateSelector<TTree, TItem> templateSelector, Func<TreeSource<TTree, TItem>, object> tagSetter = null)
        {
            return new System.Windows.HierarchicalDataTemplate(typeof(ControlTemplate<TreeSource<TTree, TItem>>)) {
                ItemsSource = new Binding("Content"),
                ItemTemplateSelector = templateSelector,
                VisualTree = GetElementFactory((obj) => folderConstructor(obj).NativeView, tagSetter)
            };
        }

    }
}
