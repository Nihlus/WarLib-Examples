using System;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;
using WarLib.BLP;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using Gdk;
using System.Collections.Generic;

public partial class MainWindow: Gtk.Window
{
	Builder builder;

	[UI] ToolButton NewFileButton;
	[UI] ToolButton OpenFileButton;
	[UI] ToolButton SaveFileButton;
	[UI] ToolButton ChooseColourButton;

	[UI] ToolButton ZoomInButton;
	[UI] ToolButton ZoomOutButton;

	[UI] Gtk.Image CurrentImage;

	[UI] Label FileTypeLabel;
	[UI] Label FileVersionLabel;
	[UI] Label CompressionTypeLabel;
	[UI] Label AlphaBitDepthLabel;
	[UI] Label PixelFormatLabel;
	[UI] Label MipCountLabel;

	[UI] Label ResolutionStatusLabel;
	[UI] Label CompressionStatusLabel;

	[UI] ImageMenuItem AboutMenuButton;

	[UI] ColorSelectionDialog BackgroundColourChooserDialog;
	[UI] AboutDialog MainAboutDialog;
[UI] MessageDialog UnsupportedImageSizeDialog;

	String currentFilePath;
	BLP currentFile;

	Pixbuf originalPixbuf;
	int zoomLevel = 0;

	List<Pixbuf> upscaledZoomLevels = new List<Pixbuf>();
	List<Pixbuf> downscaledZoomLevels = new List<Pixbuf>();


	public static MainWindow Create()
	{
		Builder builder = new Builder(null, "BLPViewer.interfaces.MainWindow.glade", null);
		return new MainWindow(builder, builder.GetObject("MainWindow").Handle);
	}

	protected MainWindow(Builder builder, IntPtr handle)
		: base(handle)
	{
		this.builder = builder;

		builder.Autoconnect(this);
		DeleteEvent += OnDeleteEvent;

		// Button Setup Start //
		NewFileButton.Clicked += OnNewFileButtonClicked;
		OpenFileButton.Clicked += OnOpenFileButtonClicked;
		SaveFileButton.Clicked += OnSaveFileButtonClicked;
		ChooseColourButton.Clicked += OnChooseColourButtonClicked;

		ZoomInButton.Clicked += OnZoomInButtonClicked;
		ZoomOutButton.Clicked += OnZoomOutButtonClicked;

		AboutMenuButton.Activated += OnAboutButtonSelected;
		// Button Setup End //

		// Image Setup Start //
		Gdk.RGBA backgroundColor = new Gdk.RGBA();       
		backgroundColor.Parse("#809fff");

		CurrentImage.OverrideBackgroundColor(StateFlags.Normal, backgroundColor);

		// Label Setup Start //
		FileTypeLabel.LabelProp = "-";
		FileVersionLabel.LabelProp = "-";
		CompressionTypeLabel.LabelProp = "-";
		AlphaBitDepthLabel.LabelProp = "-";
		PixelFormatLabel.LabelProp = "-";
		MipCountLabel.LabelProp = "-";

		ResolutionStatusLabel.LabelProp = "Resolution: No Image Loaded";
		CompressionStatusLabel.LabelProp = "";
		// Label Setup End //

		this.Title = "BLP Viewer | No Image Loaded";
	}

	protected void OnNewFileButtonClicked(object sender, EventArgs e)
	{
		FileChooserDialog fsDialog = new FileChooserDialog(
			                             "Select source file...",
			                             this, 
			                             FileChooserAction.Open, 
			                             "Cancel", ResponseType.Cancel,
			                             "Select", ResponseType.Accept);

		FileFilter jpgFiles = new FileFilter();
		jpgFiles.Name = "JPEG Images";
		jpgFiles.AddPattern("*.jpg");
		jpgFiles.AddPattern("*.jpeg");

		FileFilter pngFiles = new FileFilter();
		pngFiles.Name = "PNG Images";
		pngFiles.AddPattern("*.png");

		FileFilter gifFiles = new FileFilter();
		gifFiles.Name = "GIF Images";
		gifFiles.AddPattern("*.gif");

		FileFilter bmpFiles = new FileFilter();
		bmpFiles.Name = "Bitmap Images";
		bmpFiles.AddPattern("*.bmp");

		FileFilter tifFiles = new FileFilter();
		tifFiles.Name = "TIFF Images";
		tifFiles.AddPattern("*.tif");

		FileFilter exifFiles = new FileFilter();
		exifFiles.Name = "EXIF Images";
		exifFiles.AddPattern("*.exif");

		fsDialog.AddFilter(pngFiles);
		fsDialog.AddFilter(jpgFiles);
		fsDialog.AddFilter(gifFiles);
		fsDialog.AddFilter(bmpFiles);
		fsDialog.AddFilter(tifFiles);
		fsDialog.AddFilter(exifFiles);    

		if (fsDialog.Run() == (int)ResponseType.Accept)
		{
			if (File.Exists(fsDialog.Filename))
			{
				currentFilePath = fsDialog.Filename;

				// We're converting a normal image
				Bitmap fileImageMap = new Bitmap(currentFilePath);
				if (IsPowerOfTwo(fileImageMap.Width) && IsPowerOfTwo(fileImageMap.Height))
				{								
					currentFile = new BLP(fileImageMap, TextureCompressionType.Palettized);
				}
				else
				{
					fsDialog.Destroy ();
					UnsupportedImageSizeDialog.Run ();
					UnsupportedImageSizeDialog.Destroy ();
				}

				LoadFile(currentFilePath);
			}
		}

		fsDialog.Destroy();
	}

	protected void OnOpenFileButtonClicked(object sender, EventArgs e)
	{
		FileChooserDialog fsDialog = new FileChooserDialog(
			                             "Select a BLP file",
			                             this, 
			                             FileChooserAction.Open, 
			                             "Cancel", ResponseType.Cancel,
			                             "Open", ResponseType.Accept);

		fsDialog.Filter = new FileFilter();
		fsDialog.Filter.Name = "BLP Files";
		fsDialog.Filter.AddPattern("*.blp");

		if (fsDialog.Run() == (int)ResponseType.Accept)
		{			
			if (File.Exists(fsDialog.Filename))
			{
				currentFilePath = fsDialog.Filename;
				LoadFile(currentFilePath);
			}
		}

		fsDialog.Destroy();
	}

	private bool IsPowerOfTwo(int x)
	{
		return (x & (x - 1)) == 0;
	}

	private void LoadFile(string filePath)
	{
		if (filePath.EndsWith(".blp"))
		{
			// We're loading a BLP file
			currentFile = new BLP(File.ReadAllBytes(filePath));
		}

		if (currentFile != null)
		{
			// Load and display the BLP file at mip 0
			MemoryStream imageStream = new MemoryStream();
			Bitmap imageMap = currentFile.GetMipMap(0);
			imageMap.Save(imageStream, ImageFormat.Png);

			imageStream.Position = 0;

			Gdk.Pixbuf imageBuffer = new Gdk.Pixbuf(imageStream);
			CurrentImage.Pixbuf = imageBuffer;

			// Reset Zoom
			originalPixbuf = imageBuffer;
			upscaledZoomLevels.Clear();
			downscaledZoomLevels.Clear();
			zoomLevel = 0;

			// Load the header data into the visible info list
			FileTypeLabel.LabelProp = currentFile.GetFileType();
			FileVersionLabel.LabelProp = currentFile.GetVersion().ToString();
			CompressionTypeLabel.LabelProp = currentFile.GetCompressionType().ToString();
			AlphaBitDepthLabel.LabelProp = currentFile.GetAlphaBitDepth().ToString();
			PixelFormatLabel.LabelProp = currentFile.GetPixelFormat().ToString();
			MipCountLabel.LabelProp = currentFile.GetMipMapCount().ToString();

			ResolutionStatusLabel.LabelProp = "Resolution: " + currentFile.GetResolution().ToString();
			CompressionStatusLabel.LabelProp = currentFile.GetCompressionType().ToString();              
			// Label Setup End //

			this.Title = "BLP Viewer | " + currentFilePath;
		}
	}

	protected void OnSaveFileButtonClicked(object sender, EventArgs e)
	{           
		if (currentFile != null)
		{
			FileChooserDialog fsDialog = new FileChooserDialog(
				                             "Save to file...",
				                             this, 
				                             FileChooserAction.Save, 
				                             "Cancel", ResponseType.Cancel,
				                             "Save", ResponseType.Accept);

			FileFilter jpgFiles = new FileFilter();
			jpgFiles.Name = "JPEG Images";
			jpgFiles.AddPattern("*.jpg");
			jpgFiles.AddPattern("*.jpeg");

			FileFilter pngFiles = new FileFilter();
			pngFiles.Name = "PNG Images";
			pngFiles.AddPattern("*.png");

			FileFilter gifFiles = new FileFilter();
			gifFiles.Name = "GIF Images";
			gifFiles.AddPattern("*.gif");

			FileFilter bmpFiles = new FileFilter();
			bmpFiles.Name = "Bitmap Images";
			bmpFiles.AddPattern("*.bmp");

			FileFilter tifFiles = new FileFilter();
			tifFiles.Name = "TIFF Images";
			tifFiles.AddPattern("*.tif");

			FileFilter exifFiles = new FileFilter();
			exifFiles.Name = "EXIF Images";
			exifFiles.AddPattern("*.exif");

			FileFilter blpFiles = new FileFilter();
			blpFiles.Name = "BLP Images";
			blpFiles.AddPattern("*.blp");

			fsDialog.AddFilter(pngFiles);
			fsDialog.AddFilter(jpgFiles);
			fsDialog.AddFilter(gifFiles);
			fsDialog.AddFilter(bmpFiles);
			fsDialog.AddFilter(tifFiles);
			fsDialog.AddFilter(exifFiles);
			fsDialog.AddFilter(blpFiles);    

			if (fsDialog.Run() == (int)ResponseType.Accept)
			{
				string savePath = ValidateSavePath(fsDialog.Filename);

				if (savePath.EndsWith(".blp"))
				{
					File.WriteAllBytes(savePath, currentFile.GetBytes());
				}
				else
				{
					currentFile.GetMipMap(0).Save(savePath, GetImageFormat(savePath));
				}
			}

			fsDialog.Destroy();
		}
	}

	protected void OnChooseColourButtonClicked(object sender, EventArgs e)
	{
		if (BackgroundColourChooserDialog.Run() == (int)ResponseType.Ok)
		{
			RGBA backgroundColour = BackgroundColourChooserDialog.ColorSelection.CurrentRgba;
			CurrentImage.OverrideBackgroundColor(StateFlags.Normal, backgroundColour);
		}

		BackgroundColourChooserDialog.Hide();
	}

	private string ValidateSavePath(string path)
	{
		int extensionStart = path.LastIndexOf('.');

		if (extensionStart < 0)
		{
			string extension = ".png";
			return path + extension;
		}

		return path;
	}

	protected void OnZoomInButtonClicked(object sender, EventArgs e)
	{                
		if (CurrentImage.Pixbuf != null)
		{
			++zoomLevel;

			if (zoomLevel > 0)
			{
				// Use the upscaled pixbufs
				if (upscaledZoomLevels.ToArray().Length < zoomLevel)
				{
					// Add a new upscaled level
					upscaledZoomLevels.Add(CurrentImage.Pixbuf.ScaleSimple(originalPixbuf.Width * (zoomLevel + 1), 
							originalPixbuf.Height * (zoomLevel + 1), 
							InterpType.Hyper));
				}             

				CurrentImage.Pixbuf = upscaledZoomLevels[zoomLevel - 1];
			}
			else if (zoomLevel < 0)
			{
				if (downscaledZoomLevels.ToArray().Length < Math.Abs(zoomLevel))
				{
					// Add a new downscaled level
					downscaledZoomLevels.Add(originalPixbuf.ScaleSimple(originalPixbuf.Width / (Math.Abs(zoomLevel) + 1), 
							originalPixbuf.Height / (Math.Abs(zoomLevel) + 1), 
							InterpType.Hyper));
				}

				CurrentImage.Pixbuf = downscaledZoomLevels[Math.Abs(zoomLevel) - 1];
			}
			else
			{
				CurrentImage.Pixbuf = originalPixbuf;
			}
		}
	}

	protected void OnZoomOutButtonClicked(object sender, EventArgs e)
	{   
		if (CurrentImage.Pixbuf != null)
		{
			--zoomLevel;  
			if (zoomLevel > 0)
			{
				// Use the downscaled pixbufs
				if (upscaledZoomLevels.ToArray().Length < zoomLevel)
				{
					// Add a new upscaled level
					upscaledZoomLevels.Add(originalPixbuf.ScaleSimple(originalPixbuf.Width * (zoomLevel + 1), 
							originalPixbuf.Height * (zoomLevel + 1), 
							InterpType.Hyper));
				}             

				CurrentImage.Pixbuf = upscaledZoomLevels[zoomLevel - 1];
			}
			else if (zoomLevel < 0)
			{
				if (downscaledZoomLevels.ToArray().Length < Math.Abs(zoomLevel))
				{
					// Add a new downscaled level
					downscaledZoomLevels.Add(originalPixbuf.ScaleSimple(originalPixbuf.Width / (Math.Abs(zoomLevel) + 1), 
							originalPixbuf.Height / (Math.Abs(zoomLevel) + 1), 
							InterpType.Hyper));
				}

				CurrentImage.Pixbuf = downscaledZoomLevels[Math.Abs(zoomLevel) - 1];
			}
			else
			{
				CurrentImage.Pixbuf = originalPixbuf;
			}
		}
	}

	protected void OnAboutButtonSelected(object sender, EventArgs e)
	{
		MainAboutDialog.Run();

		MainAboutDialog.Hide();
	}

	private static ImageFormat GetImageFormat(string fileName)
	{
		string extension = System.IO.Path.GetExtension(fileName);
		if (string.IsNullOrEmpty(extension))
			throw new ArgumentException(
				string.Format("Unable to determine file extension for fileName: {0}", fileName));

		switch (extension.ToLower())
		{
			case @".bmp":
				return ImageFormat.Bmp;

			case @".gif":
				return ImageFormat.Gif;

			case @".ico":
				return ImageFormat.Icon;

			case @".jpg":
			case @".jpeg":
				return ImageFormat.Jpeg;

			case @".png":
				return ImageFormat.Png;

			case @".tif":
			case @".tiff":
				return ImageFormat.Tiff;

			case @".wmf":
				return ImageFormat.Wmf;

			default:
				throw new NotImplementedException();
		}
	}

	protected void OnDeleteEvent(object sender, DeleteEventArgs a)
	{
		Application.Quit();
		a.RetVal = true;
	}
}
