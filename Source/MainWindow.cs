// Zen Shrink - Görsel Sıkıştırıcı ve Format Dönüştürücü
// Geliştirici: Serkan ŞENTÜRK
// Telif Hakkı © 2026 Serkan ŞENTÜRK. Tüm Hakları Saklıdır.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using ComboBox = System.Windows.Controls.ComboBox;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Orientation = System.Windows.Controls.Orientation;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace ZenShrink
{
    public class ImageQueueItem
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public long OriginalSize { get; set; }
        public string Status { get; set; }
        public string ResultInfo { get; set; }
        public Border UiCard { get; set; }
        public TextBlock UiStatusText { get; set; }

        public ImageQueueItem(string path)
        {
            FilePath = path;
            FileName = Path.GetFileName(path);
            try
            {
                OriginalSize = new FileInfo(path).Length;
            }
            catch
            {
                OriginalSize = 0;
            }
            Status = "Bekliyor";
            ResultInfo = ShrinkResult.FormatBytes(OriginalSize);
        }
    }

    public class MainWindow : Window
    {
        private List<ImageQueueItem> _queue = new List<ImageQueueItem>();
        private StackPanel _queueContainer;
        private Border _dropPromptBorder;
        private TextBlock _txtSummary;
        private Slider _sliderQuality;
        private TextBlock _lblQuality;
        private ComboBox _cbFormat;
        private ComboBox _cbResize;
        private ProgressBar _progressBar;
        private Button _btnShrinkAll;
        private Button _btnClear;
        private Button _btnOpenFolder;
        private bool _isProcessing = false;
        private string _lastOutputDir = "";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Title = "Zen Shrink";
            Width = 660;
            Height = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            AllowDrop = true;

            DragOver += (s, e) =>
            {
                if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                    e.Effects = DragDropEffects.Copy;
                else
                    e.Effects = DragDropEffects.None;
                e.Handled = true;
            };

            Drop += (s, e) =>
            {
                if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                    AddFilesToQueue(files);
                }
            };

            var outerBorder = new Border
            {
                Background = new SolidColorBrush(ColorFromHex("#121217")),
                BorderBrush = new SolidColorBrush(ColorFromHex("#2C2C3D")),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(14)
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Options Panel
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // File List
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Action Bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // --- 1. HEADER ---
            var header = new Border
            {
                Background = new SolidColorBrush(ColorFromHex("#181822")),
                CornerRadius = new CornerRadius(14, 14, 0, 0),
                Padding = new Thickness(18, 12, 14, 12),
                BorderBrush = new SolidColorBrush(ColorFromHex("#272738")),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            header.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var iconText = new TextBlock
            {
                Text = "☯️",
                FontSize = 22,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var titleInfo = new StackPanel();
            var titleText = new TextBlock
            {
                Text = "Zen Shrink",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                FontFamily = new FontFamily("Segoe UI")
            };
            var subTitleText = new TextBlock
            {
                Text = "Görsel Sıkıştırıcı ve Format Dönüştürücü",
                Foreground = new SolidColorBrush(ColorFromHex("#8E8EA8")),
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI")
            };
            titleInfo.Children.Add(titleText);
            titleInfo.Children.Add(subTitleText);
            titleStack.Children.Add(iconText);
            titleStack.Children.Add(titleInfo);

            var controlStack = new StackPanel { Orientation = Orientation.Horizontal };
            var btnMin = CreateTitleButton("—", (s, e) => WindowState = WindowState.Minimized);
            var btnClose = CreateTitleButton("✕", (s, e) => Close());
            controlStack.Children.Add(btnMin);
            controlStack.Children.Add(btnClose);

            Grid.SetColumn(titleStack, 0);
            Grid.SetColumn(controlStack, 1);
            headerGrid.Children.Add(titleStack);
            headerGrid.Children.Add(controlStack);
            header.Child = headerGrid;
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // --- 2. CONFIGURATION OPTIONS CARD ---
            var optionsBorder = new Border
            {
                Margin = new Thickness(16, 12, 16, 10),
                Padding = new Thickness(16, 12, 16, 14),
                Background = new SolidColorBrush(ColorFromHex("#191924")),
                BorderBrush = new SolidColorBrush(ColorFromHex("#2C2C3F")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10)
            };

            var optionsGrid = new Grid();
            optionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            optionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Row 0: Quality slider
            var qualGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            qualGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140, GridUnitType.Pixel) });
            qualGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            qualGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50, GridUnitType.Pixel) });

            var qualLabel = new TextBlock
            {
                Text = "⚡ Sıkıştırma Kalitesi:",
                Foreground = Brushes.White,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            _sliderQuality = new Slider
            {
                Minimum = 10,
                Maximum = 100,
                Value = 80,
                SmallChange = 5,
                LargeChange = 10,
                TickFrequency = 10,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0)
            };
            _lblQuality = new TextBlock
            {
                Text = "80%",
                Foreground = new SolidColorBrush(ColorFromHex("#38BDF8")),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _sliderQuality.ValueChanged += (s, e) =>
            {
                if (_lblQuality != null)
                    _lblQuality.Text = string.Format("{0}%", (int)_sliderQuality.Value);
            };

            Grid.SetColumn(qualLabel, 0);
            Grid.SetColumn(_sliderQuality, 1);
            Grid.SetColumn(_lblQuality, 2);
            qualGrid.Children.Add(qualLabel);
            qualGrid.Children.Add(_sliderQuality);
            qualGrid.Children.Add(_lblQuality);
            Grid.SetRow(qualGrid, 0);
            optionsGrid.Children.Add(qualGrid);

            // Row 1: Target Format & Max Dimension Dropdowns
            var row1Grid = new Grid();
            row1Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16, GridUnitType.Pixel) });
            row1Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var colFormat = new StackPanel();
            var lblFmt = new TextBlock { Text = "🎯 Hedef Format:", Foreground = new SolidColorBrush(ColorFromHex("#9E9EBA")), FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
            _cbFormat = new ComboBox { Height = 28, Background = new SolidColorBrush(ColorFromHex("#222230")), Foreground = Brushes.White };
            _cbFormat.Items.Add("Orijinal Formatı Koru");
            _cbFormat.Items.Add("JPG (.jpg - En Küçük Boyut)");
            _cbFormat.Items.Add("PNG (.png - Kayıpsız)");
            _cbFormat.Items.Add("BMP (.bmp)");
            _cbFormat.SelectedIndex = 0;
            colFormat.Children.Add(lblFmt);
            colFormat.Children.Add(_cbFormat);

            var colResize = new StackPanel();
            var lblRes = new TextBlock { Text = "📐 Çözünürlük Boyutlandırma:", Foreground = new SolidColorBrush(ColorFromHex("#9E9EBA")), FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
            _cbResize = new ComboBox { Height = 28, Background = new SolidColorBrush(ColorFromHex("#222230")), Foreground = Brushes.White };
            _cbResize.Items.Add("Orijinal Çözünürlüğü Koru");
            _cbResize.Items.Add("Maks 1920px (Full HD)");
            _cbResize.Items.Add("Maks 1280px (HD)");
            _cbResize.Items.Add("Maks 1024px");
            _cbResize.Items.Add("Maks 800px (Web İdeal)");
            _cbResize.SelectedIndex = 0;
            colResize.Children.Add(lblRes);
            colResize.Children.Add(_cbResize);

            Grid.SetColumn(colFormat, 0);
            Grid.SetColumn(colResize, 2);
            row1Grid.Children.Add(colFormat);
            row1Grid.Children.Add(colResize);
            Grid.SetRow(row1Grid, 1);
            optionsGrid.Children.Add(row1Grid);

            optionsBorder.Child = optionsGrid;
            Grid.SetRow(optionsBorder, 1);
            mainGrid.Children.Add(optionsBorder);

            // --- 3. QUEUE CONTAINER / DROP ZONE ---
            var queueOuter = new Grid { Margin = new Thickness(16, 0, 16, 10) };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _queueContainer = new StackPanel();
            scroll.Content = _queueContainer;
            queueOuter.Children.Add(scroll);

            _dropPromptBorder = new Border
            {
                BorderBrush = new SolidColorBrush(ColorFromHex("#3B3B52")),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(ColorFromHex("#151520")),
                Padding = new Thickness(24)
            };
            var dropStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var dropIcon = new TextBlock { Text = "📥", FontSize = 42, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
            var dropTitle = new TextBlock { Text = "Resimleri Buraya Sürükleyin ve Bırakın", Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            var dropSub = new TextBlock { Text = "JPG, PNG, WebP, BMP veya klasör desteği", Foreground = new SolidColorBrush(ColorFromHex("#7E7E98")), FontSize = 12, Margin = new Thickness(0, 4, 0, 14), HorizontalAlignment = HorizontalAlignment.Center };
            
            var btnBrowse = new Button
            {
                Content = "📁 Resim Dosyalarını Seç",
                Padding = new Thickness(16, 8, 16, 8),
                Background = new SolidColorBrush(ColorFromHex("#2C2C3D")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            btnBrowse.Click += (s, e) => BrowseImages();

            dropStack.Children.Add(dropIcon);
            dropStack.Children.Add(dropTitle);
            dropStack.Children.Add(dropSub);
            dropStack.Children.Add(btnBrowse);
            _dropPromptBorder.Child = dropStack;
            queueOuter.Children.Add(_dropPromptBorder);

            Grid.SetRow(queueOuter, 2);
            mainGrid.Children.Add(queueOuter);

            // --- 4. ACTION BAR ---
            var actionBorder = new Border
            {
                Margin = new Thickness(16, 0, 16, 12),
                Background = Brushes.Transparent
            };
            var actionStack = new StackPanel();

            _progressBar = new ProgressBar
            {
                Height = 6,
                Margin = new Thickness(0, 0, 0, 10),
                Background = new SolidColorBrush(ColorFromHex("#22222E")),
                Foreground = new SolidColorBrush(ColorFromHex("#38BDF8")),
                BorderThickness = new Thickness(0),
                Visibility = Visibility.Collapsed
            };
            actionStack.Children.Add(_progressBar);

            var barGrid = new Grid();
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Clear
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Open Folder
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Summary
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Start Button

            _btnClear = new Button
            {
                Content = "🗑️ Temizle",
                Height = 38,
                Padding = new Thickness(12, 0, 12, 0),
                Foreground = new SolidColorBrush(ColorFromHex("#A0A0B8")),
                Background = new SolidColorBrush(ColorFromHex("#222230")),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _btnClear.Click += (s, e) => ClearQueue();

            _btnOpenFolder = new Button
            {
                Content = "📂 Klasörü Aç",
                Height = 38,
                Padding = new Thickness(12, 0, 12, 0),
                Foreground = new SolidColorBrush(ColorFromHex("#A0A0B8")),
                Background = new SolidColorBrush(ColorFromHex("#222230")),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                IsEnabled = false
            };
            _btnOpenFolder.Click += (s, e) => OpenOutputFolder();

            _txtSummary = new TextBlock
            {
                Text = "0 resim seçildi",
                Foreground = new SolidColorBrush(ColorFromHex("#8E8EA8")),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 12, 0)
            };

            _btnShrinkAll = new Button
            {
                Content = "🚀 Hepsini Sıkıştır",
                Height = 38,
                Padding = new Thickness(20, 0, 20, 0),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(ColorFromHex("#0284C7")),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _btnShrinkAll.Click += BtnShrinkAll_Click;

            Grid.SetColumn(_btnClear, 0);
            Grid.SetColumn(_btnOpenFolder, 1);
            Grid.SetColumn(_txtSummary, 2);
            Grid.SetColumn(_btnShrinkAll, 3);

            barGrid.Children.Add(_btnClear);
            barGrid.Children.Add(_btnOpenFolder);
            barGrid.Children.Add(_txtSummary);
            barGrid.Children.Add(_btnShrinkAll);
            actionStack.Children.Add(barGrid);

            actionBorder.Child = actionStack;
            Grid.SetRow(actionBorder, 3);
            mainGrid.Children.Add(actionBorder);

            // --- 5. FOOTER ---
            var footer = new Border
            {
                Background = new SolidColorBrush(ColorFromHex("#14141B")),
                BorderBrush = new SolidColorBrush(ColorFromHex("#232332")),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(18, 10, 18, 10),
                CornerRadius = new CornerRadius(0, 0, 14, 14)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var authorText = new TextBlock
            {
                Text = "Geliştirici: Serkan ŞENTÜRK | © 2026 Tüm Hakları Saklıdır.",
                Foreground = new SolidColorBrush(ColorFromHex("#6E6E82")),
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            authorText.MouseLeftButtonDown += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/836982756578") { UseShellExecute = true }); } catch { }
            };

            var verText = new TextBlock
            {
                Text = "Zen Shrink v1.0.0",
                Foreground = new SolidColorBrush(ColorFromHex("#6E6E82")),
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(authorText, 0);
            Grid.SetColumn(verText, 1);
            footerGrid.Children.Add(authorText);
            footerGrid.Children.Add(verText);
            footer.Child = footerGrid;

            Grid.SetRow(footer, 4);
            mainGrid.Children.Add(footer);

            outerBorder.Child = mainGrid;
            Content = outerBorder;
        }

        private void BrowseImages()
        {
            var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Resim Dosyaları|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.gif|Tüm Dosyalar|*.*",
                Title = "Sıkıştırılacak Resimleri Seçin"
            };
            if (dlg.ShowDialog() == true)
            {
                AddFilesToQueue(dlg.FileNames);
            }
        }

        private void AddFilesToQueue(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;

            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    try
                    {
                        string[] files = Directory.GetFiles(p, "*.*", SearchOption.AllDirectories);
                        AddFilesToQueue(files);
                    }
                    catch { }
                }
                else if (File.Exists(p) && ShrinkEngine.IsSupported(p))
                {
                    // Avoid duplicate
                    bool exists = false;
                    foreach (var item in _queue)
                    {
                        if (string.Equals(item.FilePath, p, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                    {
                        var item = new ImageQueueItem(p);
                        _queue.Add(item);
                        AddCardToUi(item);
                    }
                }
            }

            UpdateUiState();
        }

        private void AddCardToUi(ImageQueueItem item)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(ColorFromHex("#191924")),
                BorderBrush = new SolidColorBrush(ColorFromHex("#282838")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(12, 8, 12, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name & Path
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Status & Size
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Delete btn

            var iconTxt = new TextBlock
            {
                Text = "🖼️",
                FontSize = 18,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var txtName = new TextBlock
            {
                Text = item.FileName,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var txtOrig = new TextBlock
            {
                Text = ShrinkResult.FormatBytes(item.OriginalSize),
                Foreground = new SolidColorBrush(ColorFromHex("#7E7E94")),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };
            nameStack.Children.Add(txtName);
            nameStack.Children.Add(txtOrig);

            var statusTxt = new TextBlock
            {
                Text = item.Status,
                Foreground = new SolidColorBrush(ColorFromHex("#9E9EBA")),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0)
            };

            var btnDel = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Foreground = new SolidColorBrush(ColorFromHex("#8E8EA8")),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnDel.Click += (s, e) =>
            {
                if (_isProcessing) return;
                _queue.Remove(item);
                _queueContainer.Children.Remove(card);
                UpdateUiState();
            };

            item.UiCard = card;
            item.UiStatusText = statusTxt;

            Grid.SetColumn(iconTxt, 0);
            Grid.SetColumn(nameStack, 1);
            Grid.SetColumn(statusTxt, 2);
            Grid.SetColumn(btnDel, 3);

            grid.Children.Add(iconTxt);
            grid.Children.Add(nameStack);
            grid.Children.Add(statusTxt);
            grid.Children.Add(btnDel);

            card.Child = grid;
            _queueContainer.Children.Add(card);
        }

        private void UpdateUiState()
        {
            bool hasItems = _queue.Count > 0;
            _dropPromptBorder.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            _txtSummary.Text = string.Format("{0} resim listede", _queue.Count);
            _btnShrinkAll.IsEnabled = hasItems && !_isProcessing;
            _btnClear.IsEnabled = hasItems && !_isProcessing;
        }

        private void ClearQueue()
        {
            _queue.Clear();
            _queueContainer.Children.Clear();
            UpdateUiState();
        }

        private void BtnShrinkAll_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing || _queue.Count == 0) return;

            _isProcessing = true;
            _progressBar.Visibility = Visibility.Visible;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = _queue.Count;
            _progressBar.Value = 0;
            _btnShrinkAll.IsEnabled = false;
            _btnClear.IsEnabled = false;

            var options = new ShrinkOptions
            {
                Quality = (long)_sliderQuality.Value
            };

            switch (_cbFormat.SelectedIndex)
            {
                case 1: options.Format = TargetFormat.JPEG; break;
                case 2: options.Format = TargetFormat.PNG; break;
                case 3: options.Format = TargetFormat.BMP; break;
                default: options.Format = TargetFormat.KeepOriginal; break;
            }

            switch (_cbResize.SelectedIndex)
            {
                case 1: options.MaxDimension = 1920; break;
                case 2: options.MaxDimension = 1280; break;
                case 3: options.MaxDimension = 1024; break;
                case 4: options.MaxDimension = 800; break;
                default: options.MaxDimension = 0; break;
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                long totalOriginal = 0;
                long totalCompressed = 0;
                int processed = 0;

                foreach (var item in _queue)
                {
                    Dispatcher.Invoke((Action)(() =>
                    {
                        item.UiStatusText.Text = "⏳ İşleniyor...";
                        item.UiStatusText.Foreground = new SolidColorBrush(ColorFromHex("#38BDF8"));
                    }));

                    var res = ShrinkEngine.ProcessFile(item.FilePath, options);
                    processed++;

                    if (res.Success)
                    {
                        totalOriginal += res.OriginalBytes;
                        totalCompressed += res.CompressedBytes;
                        _lastOutputDir = Path.GetDirectoryName(res.OutputPath);

                        Dispatcher.Invoke((Action)(() =>
                        {
                            item.UiStatusText.Text = string.Format("✓ {0} (-{1:0.#}%)", res.GetCompressedFormatted(), res.SavedRatio);
                            item.UiStatusText.Foreground = new SolidColorBrush(ColorFromHex("#10B981"));
                            _progressBar.Value = processed;
                        }));
                    }
                    else
                    {
                        Dispatcher.Invoke((Action)(() =>
                        {
                            item.UiStatusText.Text = "✕ Hata";
                            item.UiStatusText.Foreground = new SolidColorBrush(ColorFromHex("#EF4444"));
                            _progressBar.Value = processed;
                        }));
                    }
                }

                Dispatcher.Invoke((Action)(() =>
                {
                    _isProcessing = false;
                    _progressBar.Visibility = Visibility.Collapsed;
                    _btnShrinkAll.IsEnabled = true;
                    _btnClear.IsEnabled = true;
                    _btnOpenFolder.IsEnabled = !string.IsNullOrEmpty(_lastOutputDir) && Directory.Exists(_lastOutputDir);

                    double overallRatio = totalOriginal > 0 ? (1.0 - ((double)totalCompressed / totalOriginal)) * 100.0 : 0;
                    _txtSummary.Text = string.Format("Tamamlandı: {0} ➔ {1} (-{2:0.#}%)",
                        ShrinkResult.FormatBytes(totalOriginal),
                        ShrinkResult.FormatBytes(totalCompressed),
                        overallRatio);
                }));
            });
        }

        private void OpenOutputFolder()
        {
            if (!string.IsNullOrEmpty(_lastOutputDir) && Directory.Exists(_lastOutputDir))
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", _lastOutputDir) { UseShellExecute = true });
                }
                catch { }
            }
        }

        private Button CreateTitleButton(string text, RoutedEventHandler onClick)
        {
            var btn = new Button
            {
                Content = text,
                Width = 32,
                Height = 28,
                Foreground = new SolidColorBrush(ColorFromHex("#A0A0BA")),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0)
            };
            btn.Click += onClick;
            return btn;
        }

        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
    }
}
