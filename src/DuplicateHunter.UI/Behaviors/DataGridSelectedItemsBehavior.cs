using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace DuplicateHunter.UI.Behaviors;

public static class DataGridSelectedItemsBehavior
{
    public static readonly DependencyProperty SelectedItemsSourceProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItemsSource",
            typeof(IList),
            typeof(DataGridSelectedItemsBehavior),
            new PropertyMetadata(null, OnSelectedItemsSourceChanged));

    private static readonly DependencyProperty SynchronizationProperty =
        DependencyProperty.RegisterAttached(
            "Synchronization",
            typeof(SelectionSynchronization),
            typeof(DataGridSelectedItemsBehavior),
            new PropertyMetadata(null));

    public static void SetSelectedItemsSource(DependencyObject element, IList? value)
    {
        element.SetValue(SelectedItemsSourceProperty, value);
    }

    public static IList? GetSelectedItemsSource(DependencyObject element)
    {
        return (IList?)element.GetValue(SelectedItemsSourceProperty);
    }

    private static void OnSelectedItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not System.Windows.Controls.DataGrid dataGrid)
            return;

        var synchronization = GetSynchronization(dataGrid);

        if (synchronization == null)
        {
            synchronization = new SelectionSynchronization();
            synchronization.GridSelectionChangedHandler = (_, _) => SyncFromGrid(dataGrid, synchronization);
            dataGrid.SelectionChanged += synchronization.GridSelectionChangedHandler;
            SetSynchronization(dataGrid, synchronization);
        }

        if (e.OldValue is INotifyCollectionChanged oldCollection && synchronization.SourceCollectionHandler != null)
        {
            oldCollection.CollectionChanged -= synchronization.SourceCollectionHandler;
        }

        synchronization.Source = e.NewValue as IList;
        synchronization.SourceCollection = e.NewValue as INotifyCollectionChanged;

        if (synchronization.SourceCollection != null)
        {
            synchronization.SourceCollectionHandler = (_, _) => SyncFromSource(dataGrid, synchronization);
            synchronization.SourceCollection.CollectionChanged += synchronization.SourceCollectionHandler;
        }

        SyncFromSource(dataGrid, synchronization);
    }

    private static void SyncFromGrid(System.Windows.Controls.DataGrid dataGrid, SelectionSynchronization synchronization)
    {
        if (synchronization.IsUpdating || synchronization.Source == null)
            return;

        try
        {
            synchronization.IsUpdating = true;

            // Check if lists already match
            var gridItems = dataGrid.SelectedItems.Cast<object>().ToList();
            var sourceItems = synchronization.Source.Cast<object>().ToList();

            if (gridItems.SequenceEqual(sourceItems))
                return;

            synchronization.Source.Clear();

            foreach (var item in gridItems)
            {
                synchronization.Source.Add(item);
            }
        }
        catch (InvalidOperationException)
        {
            // Suppress re-entrancy exception if collection is currently notifying
        }
        finally
        {
            synchronization.IsUpdating = false;
        }
    }

    private static void SyncFromSource(System.Windows.Controls.DataGrid dataGrid, SelectionSynchronization synchronization)
    {
        if (synchronization.IsUpdating)
            return;

        try
        {
            synchronization.IsUpdating = true;

            if (synchronization.Source == null)
            {
                dataGrid.SelectedItems.Clear();
                return;
            }

            var sourceItems = synchronization.Source.Cast<object>().ToList();
            var gridItems = dataGrid.SelectedItems.Cast<object>().ToList();

            if (sourceItems.SequenceEqual(gridItems))
                return;

            dataGrid.SelectedItems.Clear();

            foreach (var item in sourceItems)
            {
                if (dataGrid.Items.Contains(item))
                {
                    dataGrid.SelectedItems.Add(item);
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Suppress re-entrancy exception if collection is currently notifying
        }
        finally
        {
            synchronization.IsUpdating = false;
        }
    }

    private static SelectionSynchronization? GetSynchronization(DependencyObject dependencyObject)
    {
        return (SelectionSynchronization?)dependencyObject.GetValue(SynchronizationProperty);
    }

    private static void SetSynchronization(DependencyObject dependencyObject, SelectionSynchronization value)
    {
        dependencyObject.SetValue(SynchronizationProperty, value);
    }

    private sealed class SelectionSynchronization
    {
        public bool IsUpdating;
        public IList? Source;
        public INotifyCollectionChanged? SourceCollection;
        public NotifyCollectionChangedEventHandler? SourceCollectionHandler;
        public SelectionChangedEventHandler? GridSelectionChangedHandler;
    }
}