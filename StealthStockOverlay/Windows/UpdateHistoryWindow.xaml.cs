using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace StealthStockOverlay.Windows
{
    public partial class UpdateHistoryWindow : Window
    {
        private readonly List<AutoUpdater.ReleaseNote> _allReleaseNotes;

        public UpdateHistoryWindow(List<AutoUpdater.ReleaseNote> releaseNotes)
        {
            InitializeComponent();

            _allReleaseNotes = releaseNotes ?? new List<AutoUpdater.ReleaseNote>();

            BindList(_allReleaseNotes);

            if (_allReleaseNotes.Count > 0)
            {
                ReleaseListBox.SelectedIndex = 0;
            }
        }

        private void BindList(IEnumerable<AutoUpdater.ReleaseNote> notes)
        {
            ReleaseListBox.ItemsSource = null;
            ReleaseListBox.ItemsSource = notes.ToList();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var keyword = (SearchTextBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                BindList(_allReleaseNotes);
                return;
            }

            var filtered = _allReleaseNotes.Where(x =>
                (!string.IsNullOrWhiteSpace(x.version) &&
                 x.version.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.publishedAt) &&
                 x.publishedAt.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                (x.notes != null && x.notes.Any(n =>
                    !string.IsNullOrWhiteSpace(n) &&
                    n.Contains(keyword, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            BindList(filtered);

            if (filtered.Count > 0)
            {
                ReleaseListBox.SelectedIndex = 0;
            }
            else
            {
                VersionTextBlock.Text = "該当する履歴がありません";
                DetailTextBlock.Text = string.Empty;
            }
        }

        private void ReleaseListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReleaseListBox.SelectedItem is not AutoUpdater.ReleaseNote item)
            {
                return;
            }

            VersionTextBlock.Text = $"{item.version}  ({item.publishedAt})";

            var sb = new StringBuilder();

            if (item.notes != null && item.notes.Count > 0)
            {
                sb.AppendLine("更新内容");
                sb.AppendLine();

                foreach (var note in item.notes)
                {
                    sb.AppendLine($"・{note}");
                }
            }
            else
            {
                sb.Append("更新内容はありません。");
            }

            DetailTextBlock.Text = sb.ToString();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}