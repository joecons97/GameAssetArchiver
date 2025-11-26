using GameAssetArchive.Core;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GameAssetArchive.App;

public partial class MainWindow : Window
{
    private GameAssetArchiveReader? activeArchive;
    private string activeDirectory = "";

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void FileOpen_MenuItem_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Game Asset Archive Files (*.gaarc)|*.gaarc|All Files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            activeArchive?.Dispose();
            activeArchive = new GameAssetArchiveReader();
            await activeArchive.ReadFromAsync(openFileDialog.FileName);
        }

        if (activeArchive == null)
            return;

        PopulateFolderTree(FolderStructureTreeView, activeArchive.TableOfContents.Select(x => x.Key));
        UpdateActiveDirectoryList("");
    }

    private void FolderStructureTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem treeViewItem)
        {
            // Build the path from the selected item
            var pathParts = new List<string>();
            var currentItem = treeViewItem;
            while (currentItem != null)
            {
                pathParts.Insert(0, (string)currentItem.Header);
                currentItem = currentItem.Parent as TreeViewItem;
            }

            UpdateActiveDirectoryList(string.Join("/", pathParts));
        }
    }

    private void ActiveDirectoryList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ActiveDirectoryList.SelectedItem is string str)
        {
            if (str == "..")
            {
                var split = activeDirectory.Split('/');
                UpdateActiveDirectoryList(string.Join('/', split.Take(split.Length - 1)));
                return;
            }

            if (str.Split('.').Length == 2)
                return;

            UpdateActiveDirectoryList(activeDirectory.TrimEnd('/') + "/" + str);
        }
    }

    private void ActiveDirectoryList_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ActiveDirectoryList.SelectedItem is string str)
        {
            var menu = new ContextMenu();
            var item = new MenuItem()
            {
                Header = "Extract",
                IsEnabled = str.Split('.').Length == 2
            };
            menu.Items.Add(item);

            menu.IsOpen = true;

            item.Click += async (_, args) =>
            {
                foreach (var item in ActiveDirectoryList.SelectedItems)
                {
                    var path = activeDirectory.TrimEnd('/') + "/" + (item as string);

                    await ExtractItemAtPathAsync(path);
                }
            };

        }
    }

    async Task ExtractItemAtPathAsync(string fullPath)
    {
        if (activeArchive == null)
            return;

        fullPath = fullPath.TrimStart('/');

        var extension = Path.GetExtension(fullPath);
        if (string.IsNullOrEmpty(extension))
            return;

        var saveFileDialog = new SaveFileDialog
        {
            Filter = $"(*{extension})|*{extension}",
            FileName = Path.GetFileNameWithoutExtension(fullPath)
        };

        saveFileDialog.ShowDialog();

        using var data = await activeArchive.TryGetFileStreamAsync(fullPath);
        using var fileStream = File.Create(saveFileDialog.FileName);
        await data.CopyToAsync(fileStream);
    }

    void PopulateFolderTree(TreeView tree, IEnumerable<string> paths)
    {
        tree.Items.Clear();

        foreach (var path in paths)
        {
            var parts = path.Split('/', '\\');
            ItemCollection currentLevel = tree.Items;
            TreeViewItem? currentItem = null;

            foreach (var part in parts)
            {
                // Try to find an existing node
                if (part.Split('.').Length == 2)
                    continue;

                var existing = currentLevel
                    .OfType<TreeViewItem>()
                    .FirstOrDefault(i => (string)i.Header == part);

                if (existing != null)
                {
                    currentItem = existing;
                }
                else
                {
                    // Create new node
                    var newItem = new TreeViewItem
                    {
                        Header = part,
                        IsExpanded = false
                    };
                    currentLevel.Add(newItem);
                    currentItem = newItem;
                }

                // Descend into next level
                currentLevel = currentItem.Items;
            }
        }
    }

    private void UpdateActiveDirectoryList(string directory)
    {
        activeDirectory = directory;
        var items = Search(activeDirectory).ToList();
        if (directory.Contains('/'))
            items.Insert(0, "..");

        ActiveDirectoryList.ItemsSource = items;
        ActiveDirectoryList.SelectedIndex = -1;
    }

    private string[] Search(string directory)
    {
        if (activeArchive == null)
            return [];

        if (directory.StartsWith('/'))
            directory = directory.TrimStart('/');

        var paths = activeArchive.TableOfContents
            .Where(x => x.Key.StartsWith(directory))
            .Select(x => x.Key[directory.Length..])
            .ToArray();

        paths = [.. paths
            .Select(x => x.StartsWith('/') ? x : '/' + x)
            .Select(x => x.Split('/').ElementAtOrDefault(1) ?? "")
            .Where(x => string.IsNullOrEmpty(x) == false && (x.Contains('.') == false || x.Split('.', StringSplitOptions.RemoveEmptyEntries).Length == 2))
            .Distinct()];

        return paths;
    }
}