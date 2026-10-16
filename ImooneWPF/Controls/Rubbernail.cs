using ImogeIndex.Web6.Dictators;
using ImogeIndex.Mur;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImooneWPF.Controls;

/// <summary>
/// 省ｴﾈｻﾑﾈ
/// </summary>
public sealed class Rubbernail : Image
{
    public static readonly DependencyProperty LoaderProperty = DependencyProperty.RegisterAttached(
        "Loader", typeof(TileThumbnailDictator<BitmapSource>), typeof(Rubbernail),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, Changed));
    public static void SetLoader(DependencyObject target, TileThumbnailDictator<BitmapSource>? value) => target.SetValue(LoaderProperty, value);
    public static TileThumbnailDictator<BitmapSource>? GetLoader(DependencyObject target) => (TileThumbnailDictator<BitmapSource>?)target.GetValue(LoaderProperty);

    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item), typeof(FileItem), typeof(Rubbernail), new PropertyMetadata(null, Changed));

    /// <summary>
    /// ﾚｺーﾄﾞ
    /// </summary>
    public FileItem? Item {
        get => (FileItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    CancellationTokenSource? request;
    TileThumbnailDictator<BitmapSource>? subscribed;
    static readonly ImageSource Placeholder = FabricatingPlaceholder();

    public Rubbernail()
    {
        Stretch = Stretch.Uniform;
        Source = Placeholder;
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => Detach();
    }

    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is Rubbernail image) {
            image.Refresh();
        }
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Refresh();
    }

    /// <summary>
    /// もういらん
    /// </summary>
    private void Detach()
    {
        request?.Cancel();
        request?.Dispose();
        request = null;
        if (subscribed != null) {
            subscribed.Changed -= Refresh;
        }
        subscribed = null;
        Source = Placeholder;
    }

    /// <summary>
    /// 読み込み
    /// </summary>
    private async void Refresh()
    {
        Detach();
        if (!IsLoaded || Item is not { } item || GetLoader(this) is not { } loader) {
            return;
        }
        subscribed = loader;
        loader.Changed += Refresh;
        var current = new CancellationTokenSource();
        request = current;
        var size = (int)Math.Ceiling((double.IsFinite(Width) ? Width : 48) * VisualTreeHelper.GetDpi(this).DpiScaleX);
        try
        {
            var bitmap = await loader.GetAsync(item.Id, size, item.Height > item.Width, current.Token);
            if (ReferenceEquals(request, current) && ReferenceEquals(Item, item)) {
                Source = bitmap ?? Placeholder;
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// なんかWindowsのﾃﾞﾌｫｱｲｺﾝっぽいのをでっち上げる(四角に山2つ)
    /// </summary>
    /// <returns></returns>
    private static ImageSource FabricatingPlaceholder()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.SlateGray, null, new RectangleGeometry(new Rect(1, 1, 14, 14), 2, 2)));
        group.Children.Add(new GeometryDrawing(Brushes.WhiteSmoke, null, Geometry.Parse("M3,12 L7,6 L10,10 L12,8 L14,12 Z")));
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
