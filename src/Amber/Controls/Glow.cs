using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Amber.Controls;

/// <summary>
/// The soft yellow light behind every screen: Figma's 555 × 295 ellipse of #FEC960 under its 198 px layer blur,
/// centred 175.5 px below the top. Figma draws that blur like a Gaussian with σ ≈ 83 px (fitted to its screenshots).
/// Worked out once per window at 1/8 size and scaled up smoothly, so there is no blur effect and it takes
/// about 50 KB. It takes no room in the layout.
/// </summary>
public class Glow : FrameworkElement
{
    const double EllipseWidth = 555, EllipseHeight = 295, Sigma = 83, CenterY = 175.5;
    const int Step = 8, Samples = 4;

    readonly BitmapSource _image = Render();

    public Glow() => RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Linear);

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override void OnRender(DrawingContext dc)
    {
        double width = _image.PixelWidth * Step, height = _image.PixelHeight * Step;
        dc.DrawImage(_image, new Rect((ActualWidth - width) / 2, CenterY - height / 2, width, height));
    }

    static BitmapSource Render()
    {
        // The blur fades out about 3σ beyond the ellipse.
        var columns = (int)Math.Ceiling((EllipseWidth + 6 * Sigma) / Step);
        var rows = (int)Math.Ceiling((EllipseHeight + 6 * Sigma) / Step);

        // How much of each cell the ellipse covers, from a few samples per cell.
        var alpha = new double[columns * rows];
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var inside = 0;
                for (var sy = 0; sy < Samples; sy++)
                    for (var sx = 0; sx < Samples; sx++)
                    {
                        var x = (column + (sx + 0.5) / Samples - columns / 2.0) * Step / (EllipseWidth / 2);
                        var y = (row + (sy + 0.5) / Samples - rows / 2.0) * Step / (EllipseHeight / 2);
                        if (x * x + y * y <= 1) inside++;
                    }
                alpha[row * columns + column] = inside / (double)(Samples * Samples);
            }

        alpha = Blur(alpha, columns, rows, 1, columns);
        alpha = Blur(alpha, columns, rows, columns, rows);

        var pixels = new byte[alpha.Length * 4];
        for (var i = 0; i < alpha.Length; i++)
        {
            // Premultiplied BGRA
            pixels[i * 4] = (byte)Math.Round(0x60 * alpha[i]);
            pixels[i * 4 + 1] = (byte)Math.Round(0xC9 * alpha[i]);
            pixels[i * 4 + 2] = (byte)Math.Round(0xFE * alpha[i]);
            pixels[i * 4 + 3] = (byte)Math.Round(0xFF * alpha[i]);
        }
        var image = BitmapSource.Create(columns, rows, 96, 96, PixelFormats.Pbgra32, null, pixels, columns * 4);
        image.Freeze();
        return image;
    }

    /// <summary>One pass of the Gaussian blur, along rows (stride 1) or columns (stride = row length).</summary>
    static double[] Blur(double[] source, int columns, int rows, int stride, int length)
    {
        var sigma = Sigma / Step;
        var reach = (int)Math.Ceiling(3 * sigma);
        var kernel = new double[2 * reach + 1];
        for (var k = -reach; k <= reach; k++) kernel[k + reach] = Math.Exp(-k * k / (2 * sigma * sigma));
        var total = kernel.Sum();

        var result = new double[source.Length];
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var index = row * columns + column;
                var position = stride == 1 ? column : row;
                var sum = 0.0;
                for (var k = Math.Max(-reach, -position); k <= Math.Min(reach, length - 1 - position); k++)
                    sum += kernel[k + reach] * source[index + k * stride];
                result[index] = sum / total;
            }
        return result;
    }
}
