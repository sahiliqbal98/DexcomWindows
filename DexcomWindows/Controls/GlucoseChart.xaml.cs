using DexcomWindows.Models;
using DexcomWindows.Themes;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace DexcomWindows.Controls;

/// <summary>
/// Custom canvas-based glucose chart with smooth lines and interactions
/// </summary>
public sealed partial class GlucoseChart : UserControl
{
    private List<GlucoseReading> _readings = new();
    private int _targetLow = 80;
    private int _targetHigh = 160;
    private int _thresholdLow = 70;
    private int _thresholdHigh = 180;

    private const double ChartPadding = 40;
    private const double ChartTopPadding = 20;
    private const double ChartBottomPadding = 30;
    private const double PointRadius = 4;
    private const double SelectedPointRadius = 8;

    private GlucoseReading? _selectedReading;
    private List<(Point point, GlucoseReading reading)> _dataPoints = new();

    public GlucoseChart()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Update the chart with new readings
    /// </summary>
    public void SetReadings(List<GlucoseReading> readings, int targetLow = 80, int targetHigh = 160)
    {
        _readings = readings ?? new List<GlucoseReading>();
        _targetLow = targetLow;
        _targetHigh = targetHigh;
        _selectedReading = null;

        DrawChart();
    }

    /// <summary>
    /// Set threshold lines
    /// </summary>
    public void SetThresholds(int low, int high)
    {
        _thresholdLow = low;
        _thresholdHigh = high;
        DrawChart();
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawChart();
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();
        _dataPoints.Clear();

        var width = ChartCanvas.ActualWidth;
        var height = ChartCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Show empty state if no data
        if (_readings.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;

        // Calculate chart area
        var chartWidth = width - (ChartPadding * 2);
        var chartHeight = height - ChartTopPadding - ChartBottomPadding;
        var chartLeft = ChartPadding;
        var chartTop = ChartTopPadding;

        // Find value range
        var minValue = Math.Min(_readings.Min(r => r.Value), _thresholdLow - 10);
        var maxValue = Math.Max(_readings.Max(r => r.Value), _thresholdHigh + 10);
        var valueRange = maxValue - minValue;

        // Add 20 unit padding
        minValue = Math.Max(40, minValue - 20);
        maxValue = Math.Min(400, maxValue + 20);
        valueRange = maxValue - minValue;

        // Find time range
        var minTime = _readings.Min(r => r.Timestamp);
        var maxTime = _readings.Max(r => r.Timestamp);
        var timeRange = (maxTime - minTime).TotalSeconds;
        if (timeRange <= 0) timeRange = 1;

        // Draw target range band (green at 10% opacity)
        var targetBandTop = chartTop + chartHeight * (1 - (double)(_targetHigh - minValue) / valueRange);
        var targetBandBottom = chartTop + chartHeight * (1 - (double)(_targetLow - minValue) / valueRange);
        var targetBandHeight = targetBandBottom - targetBandTop;

        if (targetBandHeight > 0)
        {
            var targetBand = new Rectangle
            {
                Width = chartWidth,
                Height = targetBandHeight,
                Fill = new SolidColorBrush(Color.FromArgb(25, 52, 199, 89))
            };
            Canvas.SetLeft(targetBand, chartLeft);
            Canvas.SetTop(targetBand, targetBandTop);
            ChartCanvas.Children.Add(targetBand);
        }

        // Draw threshold lines (dashed, orange at 50% opacity)
        DrawThresholdLine(chartLeft, chartWidth, chartTop, chartHeight, minValue, valueRange, _thresholdLow);
        DrawThresholdLine(chartLeft, chartWidth, chartTop, chartHeight, minValue, valueRange, _thresholdHigh);

        // Draw Y-axis labels
        DrawYAxisLabels(chartLeft, chartTop, chartHeight, minValue, maxValue);

        // Draw X-axis time labels
        DrawXAxisLabels(chartLeft, chartWidth, chartTop + chartHeight, minTime, maxTime);

        // Calculate data points
        var points = new List<Point>();
        foreach (var reading in _readings.OrderBy(r => r.Timestamp))
        {
            var x = chartLeft + chartWidth * ((reading.Timestamp - minTime).TotalSeconds / timeRange);
            var y = chartTop + chartHeight * (1 - (double)(reading.Value - minValue) / valueRange);
            var point = new Point(x, y);
            points.Add(point);
            _dataPoints.Add((point, reading));
        }

        // Draw the line (smooth curve)
        if (points.Count > 1)
        {
            var pathGeometry = CreateSmoothPath(points);
            var linePath = new Microsoft.UI.Xaml.Shapes.Path
            {
                Data = pathGeometry,
                Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 122, 255)),
                StrokeThickness = 2.5,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            ChartCanvas.Children.Add(linePath);
        }

        // Draw data points
        foreach (var (point, reading) in _dataPoints)
        {
            var isSelected = reading == _selectedReading;
            var radius = isSelected ? SelectedPointRadius : PointRadius;

            var ellipse = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = new SolidColorBrush(ColorThemes.GetGlucoseColor(reading.ColorCategory)),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = isSelected ? 2 : 1
            };

            Canvas.SetLeft(ellipse, point.X - radius);
            Canvas.SetTop(ellipse, point.Y - radius);
            ChartCanvas.Children.Add(ellipse);
        }

        // Draw selection indicator if selected
        if (_selectedReading != null)
        {
            var selectedPoint = _dataPoints.FirstOrDefault(dp => dp.reading == _selectedReading);
            if (selectedPoint.reading != null)
            {
                // Vertical line
                var line = new Line
                {
                    X1 = selectedPoint.point.X,
                    Y1 = chartTop,
                    X2 = selectedPoint.point.X,
                    Y2 = chartTop + chartHeight,
                    Stroke = new SolidColorBrush(Color.FromArgb(100, 128, 128, 128)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 4 }
                };
                ChartCanvas.Children.Add(line);
            }
        }
    }

    private void DrawThresholdLine(double chartLeft, double chartWidth, double chartTop, double chartHeight, int minValue, double valueRange, int threshold)
    {
        var y = chartTop + chartHeight * (1 - (double)(threshold - minValue) / valueRange);

        var line = new Line
        {
            X1 = chartLeft,
            Y1 = y,
            X2 = chartLeft + chartWidth,
            Y2 = y,
            Stroke = new SolidColorBrush(Color.FromArgb(128, 255, 149, 0)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };

        ChartCanvas.Children.Add(line);
    }

    private void DrawYAxisLabels(double chartLeft, double chartTop, double chartHeight, int minValue, int maxValue)
    {
        // Use nice round numbers: 50, 100, 150, 200, 250, 300, etc.
        var niceValues = new List<int>();
        
        // Start from nearest 50 below minValue, go to nearest 50 above maxValue
        int startValue = (minValue / 50) * 50;
        int endValue = ((maxValue + 49) / 50) * 50;
        
        for (int v = startValue; v <= endValue; v += 50)
        {
            if (v >= minValue - 10 && v <= maxValue + 10)
            {
                niceValues.Add(v);
            }
        }
        
        // If too many labels, use 100 increments instead
        if (niceValues.Count > 6)
        {
            niceValues.Clear();
            startValue = (minValue / 100) * 100;
            endValue = ((maxValue + 99) / 100) * 100;
            for (int v = startValue; v <= endValue; v += 100)
            {
                if (v >= minValue - 10 && v <= maxValue + 10)
                {
                    niceValues.Add(v);
                }
            }
        }

        foreach (var value in niceValues)
        {
            var y = chartTop + chartHeight * (1 - (double)(value - minValue) / (maxValue - minValue));

            var label = new TextBlock
            {
                Text = value.ToString(),
                FontSize = 11,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI Variable"),
                Foreground = new SolidColorBrush(Color.FromArgb(180, 128, 128, 128))
            };

            Canvas.SetLeft(label, 4);
            Canvas.SetTop(label, y - 7);
            ChartCanvas.Children.Add(label);
        }
    }

    private void DrawXAxisLabels(double chartLeft, double chartWidth, double chartBottom, DateTime minTime, DateTime maxTime)
    {
        var timeSpan = maxTime - minTime;
        var totalMinutes = timeSpan.TotalMinutes;
        
        // Determine nice time intervals based on range
        int intervalMinutes;
        if (totalMinutes <= 90) // 1-1.5 hours
            intervalMinutes = 15;
        else if (totalMinutes <= 240) // up to 4 hours
            intervalMinutes = 30;
        else if (totalMinutes <= 480) // up to 8 hours
            intervalMinutes = 60;
        else if (totalMinutes <= 960) // up to 16 hours
            intervalMinutes = 120;
        else // 24 hours
            intervalMinutes = 180;
        
        // Find first nice time (rounded to interval)
        var firstNiceTime = new DateTime(
            minTime.Year, minTime.Month, minTime.Day,
            minTime.Hour, (minTime.Minute / intervalMinutes) * intervalMinutes, 0);
        
        // Move to next interval if we're past it
        if (firstNiceTime < minTime)
            firstNiceTime = firstNiceTime.AddMinutes(intervalMinutes);
        
        // Draw labels at nice intervals
        for (var time = firstNiceTime; time <= maxTime; time = time.AddMinutes(intervalMinutes))
        {
            if (time < minTime) continue;
            
            var ratio = (time - minTime).TotalSeconds / timeSpan.TotalSeconds;
            var x = chartLeft + chartWidth * ratio;
            
            // Skip if too close to edges
            if (x < chartLeft + 20 || x > chartLeft + chartWidth - 20) continue;

            var label = new TextBlock
            {
                Text = time.ToString("h:mm"),
                FontSize = 11,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI Variable"),
                Foreground = new SolidColorBrush(Color.FromArgb(180, 128, 128, 128))
            };

            Canvas.SetLeft(label, x - 15);
            Canvas.SetTop(label, chartBottom + 6);
            ChartCanvas.Children.Add(label);
        }
    }

    /// <summary>
    /// Create a smooth path using Catmull-Rom spline interpolation
    /// </summary>
    private PathGeometry CreateSmoothPath(List<Point> points)
    {
        var pathGeometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = points[0] };

        if (points.Count == 2)
        {
            // Simple line for 2 points
            figure.Segments.Add(new LineSegment { Point = points[1] });
        }
        else
        {
            // Create bezier curves for smooth interpolation
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p0 = i > 0 ? points[i - 1] : points[0];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = i < points.Count - 2 ? points[i + 2] : points[points.Count - 1];

                // Calculate control points
                var tension = 0.3;
                var cp1x = p1.X + (p2.X - p0.X) * tension;
                var cp1y = p1.Y + (p2.Y - p0.Y) * tension;
                var cp2x = p2.X - (p3.X - p1.X) * tension;
                var cp2y = p2.Y - (p3.Y - p1.Y) * tension;

                var bezier = new BezierSegment
                {
                    Point1 = new Point(cp1x, cp1y),
                    Point2 = new Point(cp2x, cp2y),
                    Point3 = p2
                };

                figure.Segments.Add(bezier);
            }
        }

        pathGeometry.Figures.Add(figure);
        return pathGeometry;
    }

    private void ChartCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(ChartCanvas).Position;

        // Find nearest data point
        GlucoseReading? nearest = null;
        double minDistance = double.MaxValue;
        Point nearestPoint = default;

        foreach (var (point, reading) in _dataPoints)
        {
            var distance = Math.Sqrt(Math.Pow(position.X - point.X, 2) + Math.Pow(position.Y - point.Y, 2));
            if (distance < minDistance && distance < 30)
            {
                minDistance = distance;
                nearest = reading;
                nearestPoint = point;
            }
        }

        if (nearest != _selectedReading)
        {
            _selectedReading = nearest;
            DrawChart();
        }

        // Update tooltip
        if (_selectedReading != null)
        {
            TooltipValue.Text = _selectedReading.Value.ToString();
            TooltipTrend.Text = _selectedReading.Trend.Symbol();
            TooltipTrend.Foreground = new SolidColorBrush(ColorThemes.GetGlucoseColor(_selectedReading.ColorCategory));
            TooltipTime.Text = _selectedReading.Timestamp.ToString("h:mm tt");

            // Position tooltip
            var tooltipX = nearestPoint.X + 10;
            var tooltipY = nearestPoint.Y - 40;

            // Keep tooltip in bounds
            if (tooltipX + 80 > ChartCanvas.ActualWidth)
                tooltipX = nearestPoint.X - 90;
            if (tooltipY < 0)
                tooltipY = nearestPoint.Y + 10;

            TooltipBorder.Margin = new Thickness(tooltipX, tooltipY, 0, 0);
            TooltipBorder.Visibility = Visibility.Visible;
        }
        else
        {
            TooltipBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void ChartCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _selectedReading = null;
        TooltipBorder.Visibility = Visibility.Collapsed;
        DrawChart();
    }
}
