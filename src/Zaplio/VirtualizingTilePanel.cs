using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Zaplio;

/// <summary>
/// Items panel that lays equal tiles out in as many columns as fit and only creates the tiles that are on screen,
/// so a list of many thousand posters stays light. With a very large <see cref="MinTileWidth"/> it is a plain one-column list.
/// Tile height = tile width × <see cref="TileAspect"/> + <see cref="TileExtraHeight"/>.
/// </summary>
public sealed class VirtualizingTilePanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty MinTileWidthProperty = DependencyProperty.Register(
        nameof(MinTileWidth), typeof(double), typeof(VirtualizingTilePanel),
        new FrameworkPropertyMetadata(136.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty TileAspectProperty = DependencyProperty.Register(
        nameof(TileAspect), typeof(double), typeof(VirtualizingTilePanel),
        new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty TileExtraHeightProperty = DependencyProperty.Register(
        nameof(TileExtraHeight), typeof(double), typeof(VirtualizingTilePanel),
        new FrameworkPropertyMetadata(56.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Narrowest a tile may get; the panel fits as many columns of at least this width as it can.</summary>
    public double MinTileWidth
    {
        get => (double)GetValue(MinTileWidthProperty);
        set => SetValue(MinTileWidthProperty, value);
    }

    public double TileAspect
    {
        get => (double)GetValue(TileAspectProperty);
        set => SetValue(TileAspectProperty, value);
    }

    public double TileExtraHeight
    {
        get => (double)GetValue(TileExtraHeightProperty);
        set => SetValue(TileExtraHeightProperty, value);
    }

    private Size _extent;
    private Size _viewport;
    private double _offset;
    private int _columns = 1;
    private double _tileWidth;
    private double _tileHeight = 1;

    /// <summary>Columns, tile size and row count for a given width and item count (pure, so it can be tested).</summary>
    internal static (int Columns, double TileWidth, double TileHeight, int Rows) Layout(double width, int count, double minTileWidth,
        double aspect, double extraHeight)
    {
        var columns = Math.Max(1, (int)(width / Math.Max(1, minTileWidth)));
        var tileWidth = Math.Floor(width / columns);
        var tileHeight = Math.Max(1, Math.Round(tileWidth * aspect + extraHeight));
        return (columns, tileWidth, tileHeight, (count + columns - 1) / columns);
    }

    /// <summary>First and last item index that must exist for the rows on screen plus one spare row above and below.</summary>
    internal static (int First, int Last) VisibleRange(double offset, double viewportHeight, double tileHeight, int columns, int count)
    {
        if (count == 0) return (0, -1);
        var firstRow = Math.Max(0, (int)(offset / tileHeight) - 1);
        var lastRow = (int)((offset + viewportHeight) / tileHeight) + 1;
        return (Math.Min(count - 1, firstRow * columns), Math.Min(count - 1, (lastRow + 1) * columns - 1));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = InternalChildren; // must be read before the generator is used
        var generator = ItemContainerGenerator;
        var count = ItemsControl.GetItemsOwner(this)?.Items.Count ?? 0;
        var width = double.IsInfinity(availableSize.Width) ? MinTileWidth * 4 : availableSize.Width;
        var (columns, tileWidth, tileHeight, rows) = Layout(width, count, MinTileWidth, TileAspect, TileExtraHeight);
        _columns = columns;
        _tileWidth = tileWidth;
        _tileHeight = tileHeight;

        var extent = new Size(width, rows * tileHeight);
        var viewport = new Size(width, double.IsInfinity(availableSize.Height) ? extent.Height : availableSize.Height);
        var offset = Math.Max(0, Math.Min(_offset, extent.Height - viewport.Height));
        var scrollChanged = extent != _extent || viewport != _viewport || offset != _offset;
        _extent = extent;
        _viewport = viewport;
        _offset = offset;

        var (first, last) = VisibleRange(_offset, _viewport.Height, tileHeight, columns, count);
        if (generator is not null)
        {
            // Drop tiles that scrolled out of range.
            for (var index = children.Count - 1; index >= 0; index--)
            {
                var position = new GeneratorPosition(index, 0);
                var item = generator.IndexFromGeneratorPosition(position);
                if (item >= first && item <= last) continue;
                generator.Remove(position, 1);
                RemoveInternalChildRange(index, 1);
            }

            if (last >= first)
            {
                var start = generator.GeneratorPositionFromIndex(first);
                var childIndex = start.Offset == 0 ? start.Index : start.Index + 1;
                using (generator.StartAt(start, GeneratorDirection.Forward, true))
                {
                    for (var item = first; item <= last; item++, childIndex++)
                    {
                        if (generator.GenerateNext(out var isNew) is not UIElement child) continue;
                        if (isNew)
                        {
                            if (childIndex >= children.Count) AddInternalChild(child);
                            else InsertInternalChild(childIndex, child);
                            generator.PrepareItemContainer(child);
                        }
                        child.Measure(new Size(tileWidth, tileHeight));
                    }
                }
            }
        }

        if (scrollChanged) ScrollOwner?.InvalidateScrollInfo();
        return new Size(width, double.IsInfinity(availableSize.Height) ? Math.Min(extent.Height, 4000) : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var generator = ItemContainerGenerator;
        if (generator is null) return finalSize;
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var item = generator.IndexFromGeneratorPosition(new GeneratorPosition(index, 0));
            if (item < 0) continue;
            InternalChildren[index].Arrange(new Rect(item % _columns * _tileWidth, item / _columns * _tileHeight - _offset, _tileWidth, _tileHeight));
        }
        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Move:
                if (args.ItemUICount > 0) RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                break;
            case NotifyCollectionChangedAction.Reset:
                _offset = 0; // a new list starts at the top
                break;
        }
        base.OnItemsChanged(sender, args);
    }

    protected override void BringIndexIntoView(int index)
    {
        var top = index / _columns * _tileHeight;
        if (top < _offset) SetVerticalOffset(top);
        else if (top + _tileHeight > _offset + _viewport.Height) SetVerticalOffset(top + _tileHeight - _viewport.Height);
    }

    // ── IScrollInfo (vertical only) ─────────────────────────────────────────────────────────────────

    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; } = true;
    public double ExtentWidth => _extent.Width;
    public double ExtentHeight => _extent.Height;
    public double ViewportWidth => _viewport.Width;
    public double ViewportHeight => _viewport.Height;
    public double HorizontalOffset => 0;
    public double VerticalOffset => _offset;
    public ScrollViewer? ScrollOwner { get; set; }

    public void LineUp() => SetVerticalOffset(_offset - 48);
    public void LineDown() => SetVerticalOffset(_offset + 48);
    public void PageUp() => SetVerticalOffset(_offset - _viewport.Height);
    public void PageDown() => SetVerticalOffset(_offset + _viewport.Height);
    public void MouseWheelUp() => SetVerticalOffset(_offset - 120);
    public void MouseWheelDown() => SetVerticalOffset(_offset + 120);
    public void LineLeft() { }
    public void LineRight() { }
    public void PageLeft() { }
    public void PageRight() { }
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
    public void SetHorizontalOffset(double offset) { }

    public void SetVerticalOffset(double offset)
    {
        offset = Math.Max(0, Math.Min(offset, _extent.Height - _viewport.Height));
        if (double.IsNaN(offset) || Math.Abs(offset - _offset) < 0.5) return;
        _offset = offset;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            if (!ReferenceEquals(InternalChildren[index], visual) && !InternalChildren[index].IsAncestorOf(visual)) continue;
            var item = ItemContainerGenerator?.IndexFromGeneratorPosition(new GeneratorPosition(index, 0)) ?? -1;
            if (item >= 0) BringIndexIntoView(item);
            break;
        }
        return rectangle;
    }
}
