using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace BiliBili.UWP.Controls
{
    public sealed partial class ImagePreview : UserControl
    {
        private Popup m_Popup;

        private List<string> _ImgUrl;
        private int _index;
        private const double ImageHorizontalPadding = 32;
        private const double ImageVerticalPadding = 128;
        private const float DefaultZoomFactor = 1.0f;
        private const float ZoomStep = 0.2f;
        private const double DragThreshold = 4.0;
        private const string PreviousButtonName = "PreviousButtonHorizontal";
        private const string NextButtonName = "NextButtonHorizontal";
        private uint? _dragPointerId;
        private UIElement _dragElement;
        private ScrollViewer _dragScrollViewer;
        private Point _dragStartPosition;
        private double _dragStartHorizontalOffset;
        private double _dragStartVerticalOffset;
        private bool _isImageDragging;
        public ImagePreview()
        {
            this.InitializeComponent();
            mainGrid.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(MainGrid_PointerWheelChanged), true);
            m_Popup = new Popup();
            this.Width = Window.Current.Bounds.Width;
            this.Height = Window.Current.Bounds.Height;
            m_Popup.Child = this;
            this.Loaded += NotifyPopup_Loaded;
            this.Unloaded += NotifyPopup_Unloaded;

        }
       
        private void NotifyPopup_Loaded(object sender, RoutedEventArgs e)
        {

            LoadImage(_ImgUrl, _index);

            Window.Current.SizeChanged += Current_SizeChanged; ;
        }
        private void LoadImage(List<string> img,int index)
        {
            List<ImageModel> ls = new List<ImageModel>();

            foreach (var item in img)
            {
                Image image = new Image() {
                    Source=new BitmapImage(new Uri(item.Replace("@300w_300h_1e_1c.jpg", "").Replace("@300w.jpg",""))),
                    HorizontalAlignment= HorizontalAlignment.Center,
                    VerticalAlignment= VerticalAlignment.Center,
                    Stretch = Stretch.Uniform
                };
                image.ImageOpened += Image_ImageOpened;
                image.ImageFailed += Image_ImageFailed;
            
                ls.Add(new ImageModel() {
                     url=item,
                    image=image
                });
            }

            imgs.ItemsSource = ls;
            imgs.SelectedIndex = index;
            UpdateImageBounds();


        }
        private void UpdateImageBounds()
        {
            var maxWidth = Math.Max(1, Width - ImageHorizontalPadding);
            var maxHeight = Math.Max(1, Height - ImageVerticalPadding);
            foreach (var item in imgs.Items)
            {
                var imageModel = item as ImageModel;
                if (imageModel?.image == null)
                {
                    continue;
                }

                imageModel.image.MaxWidth = maxWidth;
                imageModel.image.MaxHeight = maxHeight;
            }
        }
        private void Image_ImageOpened(object sender, RoutedEventArgs e)
        {
            var model = FindImageModel(sender);
            if (model != null)
            {
                model.imageFailed = false;
            }

            if ((imgs.SelectedItem as ImageModel)?.image == sender)
            {
                txt_Load.Visibility = Visibility.Collapsed;
            }
        }
        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var model = FindImageModel(sender);
            if (model != null)
            {
                model.imageFailed = true;
            }

            if ((imgs.SelectedItem as ImageModel)?.image == sender)
            {
                txt_Load.Text = "图片加载失败";
                txt_Load.Visibility = Visibility.Visible;
            }
        }
        private ImageModel FindImageModel(object image)
        {
            return imgs.Items.OfType<ImageModel>().FirstOrDefault(item => item.image == image);
        }
        private void UpdateImageLoadingState()
        {
            var model = imgs.SelectedItem as ImageModel;
            if (model?.image == null)
            {
                txt_Load.Visibility = Visibility.Collapsed;
                return;
            }

            if (model.imageFailed)
            {
                txt_Load.Text = "图片加载失败";
                txt_Load.Visibility = Visibility.Visible;
                return;
            }

            var bitmap = model.image.Source as BitmapImage;
            if (bitmap?.PixelWidth > 0)
            {
                txt_Load.Visibility = Visibility.Collapsed;
            }
            else
            {
                txt_Load.Text = "图片加载中...";
                txt_Load.Visibility = Visibility.Visible;
            }
        }
        private void MainGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            var wheelDelta = e.GetCurrentPoint(mainGrid).Properties.MouseWheelDelta;
            if (wheelDelta == 0)
            {
                return;
            }

            var source = e.OriginalSource as DependencyObject;
            if (IsControlPressed() || IsFlipViewNavigationButton(source))
            {
                ChangeSelectedImage(wheelDelta > 0 ? -1 : 1);
                e.Handled = true;
            }
        }
        private void ImageContainer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var wheelDelta = e.GetCurrentPoint(sender as UIElement).Properties.MouseWheelDelta;
            if (wheelDelta == 0)
            {
                return;
            }

            if (IsControlPressed())
            {
                ChangeSelectedImage(wheelDelta > 0 ? -1 : 1);
                e.Handled = true;
                return;
            }

            var scrollViewer = FindVisualParent<ScrollViewer>(sender as DependencyObject);
            if (scrollViewer == null)
            {
                return;
            }

            var zoomFactor = scrollViewer.ZoomFactor + (wheelDelta > 0 ? ZoomStep : -ZoomStep);
            zoomFactor = Math.Max(scrollViewer.MinZoomFactor, Math.Min(scrollViewer.MaxZoomFactor, zoomFactor));
            var zoomRatio = zoomFactor / scrollViewer.ZoomFactor;
            var viewportCenterX = scrollViewer.ViewportWidth / 2;
            var viewportCenterY = scrollViewer.ViewportHeight / 2;
            var horizontalOffset = (scrollViewer.HorizontalOffset + viewportCenterX) * zoomRatio - viewportCenterX;
            var verticalOffset = (scrollViewer.VerticalOffset + viewportCenterY) * zoomRatio - viewportCenterY;
            scrollViewer.ChangeView(horizontalOffset, verticalOffset, zoomFactor, true);
            e.Handled = true;
        }
        private void ImageContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var element = sender as UIElement;
            var scrollViewer = FindVisualParent<ScrollViewer>(element);
            var pointerPoint = e.GetCurrentPoint(element);
            if (element == null || scrollViewer == null || _dragPointerId.HasValue || !pointerPoint.Properties.IsLeftButtonPressed)
            {
                return;
            }
            if (!element.CapturePointer(e.Pointer))
            {
                return;
            }

            _dragPointerId = e.Pointer.PointerId;
            _dragElement = element;
            _dragScrollViewer = scrollViewer;
            _dragStartPosition = e.GetCurrentPoint(scrollViewer).Position;
            _dragStartHorizontalOffset = scrollViewer.HorizontalOffset;
            _dragStartVerticalOffset = scrollViewer.VerticalOffset;
            _isImageDragging = false;
            e.Handled = true;
        }
        private void ImageContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_dragPointerId != e.Pointer.PointerId || _dragScrollViewer == null)
            {
                return;
            }

            var currentPosition = e.GetCurrentPoint(_dragScrollViewer).Position;
            var horizontalDelta = currentPosition.X - _dragStartPosition.X;
            var verticalDelta = currentPosition.Y - _dragStartPosition.Y;
            if (!_isImageDragging && Math.Abs(horizontalDelta) < DragThreshold && Math.Abs(verticalDelta) < DragThreshold)
            {
                e.Handled = true;
                return;
            }

            _isImageDragging = true;
            _dragScrollViewer.ChangeView(
                _dragStartHorizontalOffset - horizontalDelta,
                _dragStartVerticalOffset - verticalDelta,
                null,
                true);
            e.Handled = true;
        }
        private void ImageContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_dragPointerId != e.Pointer.PointerId)
            {
                return;
            }

            var shouldClose = !_isImageDragging;
            _dragElement?.ReleasePointerCapture(e.Pointer);
            ClearImageDragState();
            e.Handled = true;
            if (shouldClose)
            {
                Hide();
            }
        }
        private void ImageContainer_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (_dragPointerId != e.Pointer.PointerId)
            {
                return;
            }

            _dragElement?.ReleasePointerCapture(e.Pointer);
            ClearImageDragState();
            e.Handled = true;
        }
        private void ImageContainer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (_dragPointerId == e.Pointer.PointerId)
            {
                ClearImageDragState();
            }
        }
        private void ClearImageDragState()
        {
            _dragPointerId = null;
            _dragElement = null;
            _dragScrollViewer = null;
            _isImageDragging = false;
        }
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var current = child;
            while (current != null)
            {
                var match = current as T;
                if (match != null)
                {
                    return match;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
        private static bool IsControlPressed()
        {
            var keyState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
            return (keyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        }
        private bool IsFlipViewNavigationButton(DependencyObject source)
        {
            var current = source;
            while (current != null)
            {
                var element = current as FrameworkElement;
                if (element?.Name == PreviousButtonName || element?.Name == NextButtonName)
                {
                    return true;
                }
                if (current == imgs)
                {
                    break;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }
        private void ChangeSelectedImage(int offset)
        {
            if (imgs.Items.Count == 0 || imgs.SelectedIndex < 0)
            {
                return;
            }

            var nextIndex = Math.Max(0, Math.Min(imgs.Items.Count - 1, imgs.SelectedIndex + offset));
            if (nextIndex != imgs.SelectedIndex)
            {
                imgs.SelectedIndex = nextIndex;
            }
        }
        private void ResetImageZoom(int index)
        {
            if (index < 0)
            {
                return;
            }

            var container = imgs.ContainerFromIndex(index) as DependencyObject;
            var scrollViewer = FindVisualChild<ScrollViewer>(container);
            scrollViewer?.ChangeView(0, 0, DefaultZoomFactor, true);
        }
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var match = child as T ?? FindVisualChild<T>(child);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }
        private void SbOut_Completed(object sender, object e)
        {
            this.m_Popup.IsOpen = false;
        }
        private void Current_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            this.Width = e.Size.Width;
            this.Height = e.Size.Height;
            UpdateImageBounds();
        }

        private void NotifyPopup_Unloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.SizeChanged -= Current_SizeChanged;
        }


        public ImagePreview(List<string> url,int index) : this()
        {
            this._ImgUrl = url;
            _index = index;
            txt_Load.Text = "图片加载中...";
            imgs.ItemsSource = null;
        }



        public void Show()
        {
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                mainGrid.Margin = new Thickness(0, 24, 0, 0);
            }

            this.m_Popup.IsOpen = true;
            this.sbIn.Begin();
        }
        private void Hide()
        {

            this.sbOut.Begin();
            this.sbOut.Completed += SbOut_Completed1;


        }

        private void SbOut_Completed1(object sender, object e)
        {
            this.m_Popup.IsOpen = false;
        }


    
     
        private void sv1_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (FindVisualParent<ScrollViewer>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }
            Hide();
        }

        private void btn_Close_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private async void btn_Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileSavePicker save = new FileSavePicker();
                save.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                save.FileTypeChoices.Add("图片", new List<string>() { ".jpg" });
                save.SuggestedFileName = "bilibili_img_" + DateTime.Now.ToString();
                StorageFile file = await save.PickSaveFileAsync();
                if (file != null)
                {
                    //img_Image
                    var u = (imgs.SelectedItem as ImageModel).url.Replace("@300w_300h_1e_1c.jpg", "").Replace("@300w.jpg", "");
                    IBuffer bu = await WebClientClass.GetBuffer(new Uri(u));
                    CachedFileManager.DeferUpdates(file);
                    await FileIO.WriteBufferAsync(file, bu);
                    FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                    Utils.ShowMessageToast("保存成功");
                }

            }
            catch (Exception ex)
            {
               
                Utils.ShowMessageToast("保存失败");
            }
        }
        int RotateNum = 1;
        private void btn_Rotate_Click(object sender, RoutedEventArgs e)
        {
            if (RotateNum == 4)
            {
                RotateNum = 0;
            }
            CompositeTransform compositeTransform = new CompositeTransform()
            {
                Rotation = 90 * RotateNum
            };

            var imageViews=(imgs.SelectedItem as ImageModel).image;

            imageViews.RenderTransformOrigin = new Point(0.5, 0.5);
            imageViews.RenderTransform = compositeTransform;
            RotateNum++;
        }
        float ZoomFactor = (float)1.0;
        private void btn_ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ZoomFactor += (float)0.2;
          
           //sv1.ChangeView(null, null, ZoomFactor);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ZoomFactor -= (float)0.2;
            //sv1.ChangeView(null, null, ZoomFactor);
        }

        private void btn_Share_Click(object sender, RoutedEventArgs e)
        {
            //DataPackage dataPackage = new DataPackage();
            //RandomAccessStreamReference randomAccessStreamReference =
            //    RandomAccessStreamReference.CreateFromStream(_bitimg);
            //dataPackage.SetBitmap(randomAccessStreamReference);

            //Clipboard.SetContent(dataPackage);
            //Utils.ShowMessageToast("已将图片复制到剪贴板");
            //DataTransferManager.ShowShareUI();
        }


        public class ImageModel
        {
            public string url { get; set; }
            public Image image { get; set; }
            public bool imageFailed { get; set; }
        }

        private async void imgs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            txt_Count.Text = imgs.Items.Count == 0 ? "" : (imgs.SelectedIndex + 1) + "/" + imgs.Items.Count;
            var selectedIndex = imgs.SelectedIndex;
            UpdateImageLoadingState();
            if (selectedIndex >= 0)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => ResetImageZoom(selectedIndex));
            }
        }
    }
}
