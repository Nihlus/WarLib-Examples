using System;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;
using System.IO;
using WarLib.MPQ;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public partial class MainWindow: Gtk.Window
{
	Builder builder;

	[UI] Gtk.ToolButton NewArchiveButton;
	[UI] Gtk.ToolButton OpenArchiveButton;
	[UI] Gtk.ToolButton SaveArchiveButton;

	[UI] TreeStore ArchiveTreeStore;
	[UI] TreeView ArchiveTreeView;

	[UI] Menu FileContextMenu;

	[UI] ImageMenuItem ExtractItem;
	[UI] ImageMenuItem OpenItem;
	[UI] ImageMenuItem CopyPathItem;

	Stream currentFileStream = null;
	MPQ currentMPQ = null;

	private Dictionary<string, List<string>> folderDictionary = new Dictionary<string, List<string>>();
	private Dictionary<string, List<string>> folderContentDictionary = new Dictionary<string, List<string>>();
	private Dictionary<string, TreeIter> folderNodeDictionary = new Dictionary<string, TreeIter>();

	List<string> listFile;
	string currentPath;

	public static MainWindow Create()
	{
		Builder builder = new Builder(null, "MPQExplorer.interfaces.MainWindow.glade", null);
		return new MainWindow(builder, builder.GetObject("window1").Handle);
	}

	protected MainWindow(Builder builder, IntPtr handle)
		: base(handle)
	{
		this.builder = builder;

		builder.Autoconnect(this);
		DeleteEvent += OnDeleteEvent;

		OpenArchiveButton.Clicked += OnOpenArchiveButtonClicked;
		ArchiveTreeView.RowExpanded += OnArchiveRowExpanded;
		ArchiveTreeView.ButtonPressEvent += OnArchiveTreeClicked;

		ExtractItem.Activated += OnItemExtract;
		OpenItem.Activated += OnItemOpen;
		CopyPathItem.Activated += OnItemCopyPath;
	}

	protected void OnOpenArchiveButtonClicked(object sender, EventArgs e)
	{
		FileChooserDialog fsDialog = new FileChooserDialog(
			                             "Select an MPQ archive",
			                             this, 
			                             FileChooserAction.Open, 
			                             "Cancel", ResponseType.Cancel,
			                             "Open", ResponseType.Accept);

		fsDialog.Filter = new FileFilter();
		fsDialog.Filter.Name = "MoPaQ Archives";
		fsDialog.Filter.AddPattern("*.mpq");
		fsDialog.Filter.AddPattern("*.MPQ");

		if (fsDialog.Run() == (int)ResponseType.Accept)
		{
			string currentFilePath = fsDialog.Filename;
			if (File.Exists(fsDialog.Filename))
			{
				if (currentMPQ != null)
				{
					currentMPQ.Dispose();
				}

				if (currentFileStream != null)
				{
					currentFileStream.Dispose();
				}

				currentFileStream = File.OpenRead(currentFilePath);
				currentMPQ = new MPQ(currentFileStream);

				if (currentMPQ.HasFileList())
				{
					// Load MPQ file list
					listFile = currentMPQ.GetFileList();

					fsDialog.Destroy();
					BuildFileTree();
				}
				else
				{
					MessageDialog dialog = new MessageDialog(this, DialogFlags.Modal, MessageType.Warning, ButtonsType.Ok, 
						                       String.Format("This MPQ archive doesn't contain a listfile. Without this file, it is impossible to read any files from the archive.\n\n" +
							                       "You can manually select an external listfile to be used for the archive."));

					dialog.Run();
					dialog.Destroy();
				}
			}
		}

		fsDialog.Destroy();
	}

	protected void OnArchiveRowExpanded(object sender, RowExpandedArgs e)
	{		
		// Whenever a row is expanded, find the subfolders in the dictionary
		// Enumerate the files and subfolders in those.

		string folderPath = GetFilePathFromIter(e.Iter);
		if (folderDictionary.ContainsKey(folderPath))
		{
			foreach (string folder in folderDictionary[folderPath])
			{
				EnumerateDirectories(folderPath + folder);
			}
		}
	}

	protected void OnItemExtract(object sender, EventArgs e)
	{
		FileChooserDialog fsDialog = new FileChooserDialog(
			                             "Save file",
			                             this, 
			                             FileChooserAction.Save, 
			                             "Cancel", ResponseType.Cancel,
			                             "Save", ResponseType.Accept);

		fsDialog.SetFilename(System.IO.Path.GetFileName(currentPath));

		if (fsDialog.Run() == (int)ResponseType.Accept)
		{
			File.WriteAllBytes(fsDialog.Filename, currentMPQ.ExtractFile(currentPath));
		}

		fsDialog.Destroy();
	}

	protected void OnItemOpen(object sender, EventArgs e)
	{
		
	}

	protected void OnItemCopyPath(object sender, EventArgs e)
	{
		Clipboard clipboard = Clipboard.Get(Gdk.Atom.Intern("CLIPBOARD", false));
		clipboard.Text = currentPath;
	}

	[GLib.ConnectBefore]
	protected void OnArchiveTreeClicked(object sender, ButtonPressEventArgs e)
	{
		TreePath path;
		ArchiveTreeView.GetPathAtPos((int)e.Event.X, (int)e.Event.Y, out path);

		TreeIter iter;
		ArchiveTreeStore.GetIterFromString(out iter, path.ToString());

		currentPath = GetFilePathFromIter(iter);

		if (e.Event.Type == Gdk.EventType.ButtonPress && e.Event.Button == 3)
		{
			Console.WriteLine("Rightclick in tree: ");
			FileContextMenu.ShowAll();
			FileContextMenu.Popup();
		}
	}

	private void BuildFileTree()
	{
		// Reset the dictionary
		folderDictionary.Clear();
		folderContentDictionary.Clear();
		folderNodeDictionary.Clear();

		// Clear the stored data
		ArchiveTreeStore.Clear();

		// Find the top level directories
		EnumerateDirectories();

		// Enumerate files and subfolders in those
		foreach (string path in folderDictionary[""])
		{
			EnumerateDirectories(path);
		}
	}

	public void EnumerateDirectories(string parent = "")
	{
		// Grab a list of all the directory names in the specified parent directory inside the listfile
		if (!String.IsNullOrWhiteSpace(parent))
		{
			// We're enumerating an existing directory, so we can drop the search as soon as the parent doesn't match anymore
			List<string> topLevelDirectories = new List<string>();
			List<string> folderFileContent = new List<string>();
			bool bHasFoundStartOfFolderBlock = false;

			foreach (string path in listFile)
			{
				if (path.StartsWith(parent))
				{
					bHasFoundStartOfFolderBlock = true;
					// Now we can start reading stuff

					// Remove the parent from the directory line
					string strippedPath = Regex.Replace(path, "^" + Regex.Escape(parent), "");

					// Get the top folders
					int slashIndex = strippedPath.IndexOf('\\');
					string topDirectory = strippedPath.Substring(0, slashIndex + 1);

					if (!String.IsNullOrEmpty(topDirectory) && !topLevelDirectories.Contains(topDirectory) && !folderNodeDictionary.ContainsKey(parent + topDirectory))
					{
						topLevelDirectories.Add(topDirectory);
						folderNodeDictionary.Add(parent + topDirectory, AddDirectoryNode(parent, topDirectory));
					}
					else if (String.IsNullOrEmpty(topDirectory) && slashIndex == -1 && IsFile(strippedPath))
					{
						// We've found a file!
						folderFileContent.Add(strippedPath);
						AddFileNode(parent, strippedPath);
					}
				}
				else if (bHasFoundStartOfFolderBlock)
				{
					// We've read all the entries we need to, so abort
					break;
				}
			}

			if (topLevelDirectories.Count > 0)
			{
				if (!folderDictionary.ContainsKey(parent))
				{
					folderDictionary.Add(parent, topLevelDirectories);
				}
			}

			if (folderFileContent.Count > 0)
			{
				if (!folderContentDictionary.ContainsKey(parent))
				{
					folderContentDictionary.Add(parent, folderFileContent);
				}
			}
		}
		else
		{
			// We're enumerating the top-level directories, so all paths are relevant
			List<string> topLevelDirectories = new List<string>();
			List<string> folderFileContent = new List<string>();

			foreach (string path in listFile)
			{											
				int slashIndex = path.IndexOf('\\');
				string topDirectory = path.Substring(0, slashIndex + 1);

				if (!String.IsNullOrEmpty(topDirectory) && !topLevelDirectories.Contains(topDirectory))
				{
					topLevelDirectories.Add(topDirectory);
					folderNodeDictionary.Add(parent + topDirectory, AddDirectoryNode(parent, topDirectory));
				}
				else if (String.IsNullOrEmpty(topDirectory) && slashIndex == -1)
				{
					// Remove the parent from the directory line
					string strippedPath = Regex.Replace(path, "^" + Regex.Escape(parent), "");	

					if (IsFile(strippedPath))
					{
						// We've found a file!
						folderFileContent.Add(strippedPath);
						AddFileNode(parent, strippedPath);
					}
				}							
			}

			if (topLevelDirectories.Count > 0)
			{
				folderDictionary.Add(parent, topLevelDirectories);
			}

			if (folderFileContent.Count > 0)
			{
				folderContentDictionary.Add(parent, folderFileContent);
			}
		}
	}

	private bool IsFile(string name)
	{
		string[] parts = name.Split('.');
		return parts.Length > 1;
	}

	private TreeIter AddDirectoryNode(string parentNodeKey, string directoryName)
	{
		TreeIter parentNode = new TreeIter();
		folderNodeDictionary.TryGetValue(parentNodeKey, out parentNode);

		if (ArchiveTreeStore.IterIsValid(parentNode))
		{
			// Add myself to that node
			return ArchiveTreeStore.AppendValues(parentNode, Stock.Directory, directoryName.Replace("\\", ""));

		}
		else
		{
			// I'm a new root node					
			return ArchiveTreeStore.AppendValues(Stock.Directory, directoryName.Replace("\\", ""));
		}
	}

	private TreeIter AddFileNode(string parentNodeKey, string fileName)
	{
		TreeIter parentNode = new TreeIter();
		folderNodeDictionary.TryGetValue(parentNodeKey, out parentNode);

		if (ArchiveTreeStore.IterIsValid(parentNode))
		{
			// Add myself to that node
			return ArchiveTreeStore.AppendValues(parentNode, Stock.File, fileName);

		}
		else
		{
			// I'm a new root node					
			return ArchiveTreeStore.AppendValues(Stock.File, fileName);
		}
	}

	private string GetFilePathFromIter(TreeIter iter)
	{
		TreeIter parentIter = new TreeIter();
		string finalPath = "";

		ArchiveTreeStore.IterParent(out parentIter, iter);
		if (ArchiveTreeStore.IterIsValid(parentIter))
		{
			finalPath = GetFilePathFromIter(parentIter) + (string)ArchiveTreeStore.GetValue(iter, 1);

		}
		else
		{
			finalPath = (string)ArchiveTreeStore.GetValue(iter, 1);
		}

		if (!IsFile(finalPath))
		{
			finalPath += "\\";
		}

		return finalPath;
	}

	protected void OnDeleteEvent(object sender, DeleteEventArgs a)
	{
		if (currentMPQ != null)
		{
			currentMPQ.Dispose();
		}

		if (currentFileStream != null)
		{
			currentFileStream.Dispose();
		}

		Application.Quit();
		a.RetVal = true;
	}
}
