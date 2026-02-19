#region Copyright Kayomani 2010.  Licensed under the GPLv3 (Or later version), Expand for details. Do not remove this notice.
/**
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or any 
    later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FAP.Application.Views;
using FAP.Domain.Entities.FileSystem;
using FAP.Application.ViewModels;

namespace Fap.Presentation.Panels
{
    public partial class BrowsePanel : UserControl, IBrowserView
    {
        public BrowsePanel()
        {
            InitializeComponent();
            DataContextChanged += BrowsePanel_DataContextChanged;
        }

        private BrowserViewModel? Model => DataContext as BrowserViewModel;

        private void BrowsePanel_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Model != null)
            {
                bar.PathChanged += Bar_PathChanged;
                Model.PropertyChanged += Model_PropertyChanged;

                if (Model.Root.Count == 0)
                    Model.IsBusy = true;
            }
        }

        private void Bar_PathChanged(object sender, RoutedPropertyChangedEventArgs<string> e)
        {
            if (Model != null && bar.Path != Model.CurrentPath)
            {
                Model.CurrentPath = bar.Path;
            }
        }

        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Model != null && listView2.SelectedItems != null)
            {
                Model.LastSelectedEntity = listView2.SelectedItems.Cast<BrowsingFile>().ToList();
            }
        }

        private void foldersTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ignoreFolderTreeEvents)
                return;
            BrowsingFile? ent = e.NewValue as BrowsingFile;
            if (Model == null) return;

            if (Model.LastSelectedEntity == null)
                Model.LastSelectedEntity = new List<BrowsingFile>();
            Model.LastSelectedEntity.Clear();
            if (ent != null)
            {
                Model.LastSelectedEntity.Add(ent);
                Model.CurrentPath = ent.FullPath;
            }
        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (ignoreFolderTreeEvents)
                return;
            TreeViewItem? src = e.OriginalSource as TreeViewItem;
            BrowsingFile? ent = src?.DataContext as BrowsingFile;
            if (ent != null && Model != null)
            {
                Model.CurrentPath = ent.FullPath;
            }
        }

        private void listView2_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            BrowsingFile? item = listView2.SelectedItem as BrowsingFile;

            if (item != null && Model != null)
            {
                if (item.IsFolder)
                {
                    Model.CurrentPath = item.FullPath;
                    var container = foldersTree.ContainerFromItem(item);
                    if (container != null)
                        container.IsExpanded = true;
                }
                else
                {
                    Model.LastSelectedEntity = new List<BrowsingFile> { item };
                    Model.Download.Execute(null);
                }
            }
        }

        private childItem? FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is childItem typedChild)
                    return typedChild;

                childItem? childOfChild = FindVisualChild<childItem>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        private void Model_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (Model == null) return;

            if (e.PropertyName == "CurrentPath")
            {
                if (Model.CurrentItem != null && Model.CurrentPath != bar.Path)
                {
                    bar.PathChanged -= Bar_PathChanged;
                    bar.Path = Model.CurrentPath.Replace('/', '\\');
                    bar.PathChanged += Bar_PathChanged;
                }

                ignoreFolderTreeEvents = true;
                var entity = GetEntityFromPath(Model.CurrentPath);
                var container = foldersTree.ContainerFromItem(entity);
                if (container != null)
                {
                    container.IsSelected = true;
                    container.BringIntoView();
                }
                ignoreFolderTreeEvents = false;
            }
            else if (e.PropertyName == "IsBusy")
            {
                bar.IsProgressActive = Model.IsBusy;
            }
            else if (e.PropertyName == "CurrentItem")
            {
                ScrollViewer? scrollViewer = FindVisualChild<ScrollViewer>(listView2);
                scrollViewer?.ScrollToTop();
            }
        }

        private bool ignoreFolderTreeEvents;

        private BrowsingFile? GetEntityFromPath(string path)
        {
            if (string.IsNullOrEmpty(path) || Model == null)
                return null;

            string[] items = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            BrowsingFile? parent = Model.Root.FirstOrDefault(n => n.Name == items[0]);

            for (int i = 1; i < items.Length && parent != null; i++)
            {
                var search = parent.Items.FirstOrDefault(n => n.Name == items[i]);
                if (search == null)
                    return null;
                parent = search;
            }

            return parent;
        }

        private void listView2_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (listView2.SelectedItems != null && listView2.SelectedItems.Count > 0)
            {
                if (listView2.ContextMenu.Items.Count == 0)
                {
                    MenuItem m = new MenuItem
                    {
                        Foreground = System.Windows.Media.Brushes.Black,
                        Header = "Download",
                        Command = Model?.Download
                    };
                    listView2.ContextMenu.Items.Add(m);
                }
            }
            else
            {
                listView2.ContextMenu.Items.Clear();
                e.Handled = true;
            }
        }
    }

    public static class TreeViewExtensions
    {
        public static TreeViewItem? ContainerFromItem(this TreeView treeView, object? item)
        {
            if (item == null) return null;

            TreeViewItem? containerThatMightContainItem = 
                (TreeViewItem?)treeView.ItemContainerGenerator.ContainerFromItem(item);
            if (containerThatMightContainItem != null)
                return containerThatMightContainItem;
            else
                return ContainerFromItem(treeView.ItemContainerGenerator, treeView.Items, item);
        }

        private static TreeViewItem? ContainerFromItem(
            ItemContainerGenerator parentItemContainerGenerator, ItemCollection itemCollection, object item)
        {
            foreach (object curChildItem in itemCollection)
            {
                TreeViewItem? parentContainer = 
                    (TreeViewItem?)parentItemContainerGenerator.ContainerFromItem(curChildItem);
                if (parentContainer == null)
                    return null;

                TreeViewItem? containerThatMightContainItem = 
                    (TreeViewItem?)parentContainer.ItemContainerGenerator.ContainerFromItem(item);
                if (containerThatMightContainItem != null)
                    return containerThatMightContainItem;

                TreeViewItem? recursionResult = 
                    ContainerFromItem(parentContainer.ItemContainerGenerator, parentContainer.Items, item);
                if (recursionResult != null)
                    return recursionResult;
            }
            return null;
        }

        public static object? ItemFromContainer(this TreeView treeView, TreeViewItem container)
        {
            TreeViewItem? itemThatMightBelongToContainer = 
                (TreeViewItem?)treeView.ItemContainerGenerator.ItemFromContainer(container);
            if (itemThatMightBelongToContainer != null)
                return itemThatMightBelongToContainer;
            else
                return ItemFromContainer(treeView.ItemContainerGenerator, treeView.Items, container);
        }

        private static object? ItemFromContainer(
            ItemContainerGenerator parentItemContainerGenerator, ItemCollection itemCollection, TreeViewItem container)
        {
            foreach (object curChildItem in itemCollection)
            {
                TreeViewItem? parentContainer = 
                    (TreeViewItem?)parentItemContainerGenerator.ContainerFromItem(curChildItem);
                if (parentContainer == null) continue;

                TreeViewItem? itemThatMightBelongToContainer = 
                    (TreeViewItem?)parentContainer.ItemContainerGenerator.ItemFromContainer(container);
                if (itemThatMightBelongToContainer != null)
                    return itemThatMightBelongToContainer;

                TreeViewItem? recursionResult = 
                    ItemFromContainer(parentContainer.ItemContainerGenerator, parentContainer.Items, container) as TreeViewItem;
                if (recursionResult != null)
                    return recursionResult;
            }
            return null;
        }
    }
}
