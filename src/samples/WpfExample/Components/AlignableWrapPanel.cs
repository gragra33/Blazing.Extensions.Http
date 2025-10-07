using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfExample.Components;

/// <summary>
/// A wrap panel that supports horizontal content alignment.
/// </summary>
internal sealed class AlignableWrapPanel : Panel
{
    /// <summary>
    /// Gets or sets the horizontal content alignment.
    /// </summary>
    public HorizontalAlignment HorizontalContentAlignment
    {
        get => (HorizontalAlignment)GetValue(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    /// <summary>
    /// Dependency property for <see cref="HorizontalContentAlignment"/>.
    /// </summary>
    public static readonly DependencyProperty HorizontalContentAlignmentProperty =
        DependencyProperty.Register(
            nameof(HorizontalContentAlignment), 
            typeof(HorizontalAlignment), 
            typeof(AlignableWrapPanel), 
            new FrameworkPropertyMetadata(HorizontalAlignment.Left, FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>
    /// Measures the child elements and returns the desired size for this panel.
    /// </summary>
    /// <param name="constraint">The available size that this element can give to child elements.</param>
    /// <returns>The size that this panel determines it needs during layout.</returns>
    protected override Size MeasureOverride(Size constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);

        Size curLineSize = new Size();
        Size panelSize = new Size();
        UIElementCollection children = InternalChildren;

        for (int i = 0; i < children.Count; i++)
        {
            UIElement child = children[i];

            // Flow passes its own constraint to children
            child.Measure(constraint);
            Size sz = child.DesiredSize;

            if (curLineSize.Width + sz.Width > constraint.Width) // need to switch to another line
            {
                panelSize.Width = Math.Max(curLineSize.Width, panelSize.Width);
                panelSize.Height += curLineSize.Height;
                curLineSize = sz;

                if (sz.Width > constraint.Width) // if the element is wider than the constraint - give it a separate line                    
                {
                    panelSize.Width = Math.Max(sz.Width, panelSize.Width);
                    panelSize.Height += sz.Height;
                    curLineSize = new Size();
                }
            }
            else // continue to accumulate a line
            {
                curLineSize.Width += sz.Width;
                curLineSize.Height = Math.Max(sz.Height, curLineSize.Height);
            }
        }

        // the last line size, if any need to be added
        panelSize.Width = Math.Max(curLineSize.Width, panelSize.Width);
        panelSize.Height += curLineSize.Height;

        return panelSize;
    }

    /// <summary>
    /// Arranges the child elements and returns the final size used by this panel.
    /// </summary>
    /// <param name="arrangeBounds">The final area within the parent that this element should use to arrange itself and its children.</param>
    /// <returns>The actual size used.</returns>
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        ArgumentNullException.ThrowIfNull(arrangeBounds);

        int firstInLine = 0;
        Size curLineSize = new Size();
        double accumulatedHeight = 0;
        UIElementCollection children = InternalChildren;

        for (int i = 0; i < children.Count; i++)
        {
            Size sz = children[i].DesiredSize;

            if (curLineSize.Width + sz.Width > arrangeBounds.Width) // need to switch to another line
            {
                ArrangeLine(accumulatedHeight, curLineSize, arrangeBounds.Width, firstInLine, i);

                accumulatedHeight += curLineSize.Height;
                curLineSize = sz;

                if (sz.Width > arrangeBounds.Width) // the element is wider than the constraint - give it a separate line                    
                {
                    ArrangeLine(accumulatedHeight, sz, arrangeBounds.Width, i, ++i);
                    accumulatedHeight += sz.Height;
                    curLineSize = new Size();
                }
                firstInLine = i;
            }
            else // continue to accumulate a line
            {
                curLineSize.Width += sz.Width;
                curLineSize.Height = Math.Max(sz.Height, curLineSize.Height);
            }
        }

        if (firstInLine < children.Count)
        {
            ArrangeLine(accumulatedHeight, curLineSize, arrangeBounds.Width, firstInLine, children.Count);
        }

        return arrangeBounds;
    }

    /// <summary>
    /// Arranges a single line of children.
    /// </summary>
    /// <param name="y">The Y position of the line.</param>
    /// <param name="lineSize">The size of the line.</param>
    /// <param name="boundsWidth">The width of the bounds.</param>
    /// <param name="start">The start index of children in the line.</param>
    /// <param name="end">The end index of children in the line.</param>
    private void ArrangeLine(double y, Size lineSize, double boundsWidth, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(lineSize);

        double x = 0;
        if (HorizontalContentAlignment == HorizontalAlignment.Center)
        {
            x = (boundsWidth - lineSize.Width) / 2;
        }
        else if (HorizontalContentAlignment == HorizontalAlignment.Right)
        {
            x = boundsWidth - lineSize.Width;
        }

        UIElementCollection children = InternalChildren;
        for (int i = start; i < end; i++)
        {
            UIElement child = children[i];
            child.Arrange(new Rect(x, y, child.DesiredSize.Width, lineSize.Height));
            x += child.DesiredSize.Width;
        }
    }
}