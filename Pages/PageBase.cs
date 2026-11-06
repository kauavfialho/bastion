using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Bastion.Core;

namespace Bastion.Pages
{
    /// <summary>Página com título, subtítulo, linha de status e corpo rolável.</summary>
    public abstract class PageBase : UserControl
    {
        protected readonly StackPanel Body = new() { Margin = new Thickness(0, 22, 0, 0) };
        readonly TextBlock status;

        protected PageBase(string title, string subtitle)
        {
            var root = new StackPanel { Margin = new Thickness(38, 32, 38, 32), MaxWidth = 1100, HorizontalAlignment = HorizontalAlignment.Stretch };
            root.Children.Add(Ui.T(title, 28, weight: FontWeights.SemiBold));
            root.Children.Add(Ui.T(subtitle, 14, Ui.Sub, margin: new Thickness(0, 6, 0, 0)));
            status = Ui.T("", 12.5, Ui.Accent, margin: new Thickness(0, 8, 0, 0));
            status.Visibility = Visibility.Collapsed;
            root.Children.Add(status);
            root.Children.Add(Body);
            Content = new ScrollViewer
            {
                Content = root,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
        }

        public virtual Task LoadAsync() => Task.CompletedTask;

        protected void Status(string text)
        {
            status.Text = I18n.Tr(text);
            status.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>Mostra uma mensagem enquanto a tarefa roda e trata erros.</summary>
        protected async Task Busy(string message, Func<Task> work)
        {
            Status(message);
            try
            {
                await work();
                Status("");
            }
            catch (Exception ex)
            {
                Status("Erro: " + ex.Message);
            }
        }

        protected static UIElement AdminNote(string what) =>
            Admin.IsAdmin ? new Border() : Ui.Hint($"Abra como administrador para {what}.", new Thickness(0, 0, 0, 8));
    }
}
