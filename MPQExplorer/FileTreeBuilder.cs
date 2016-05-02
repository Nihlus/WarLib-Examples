using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace MPQExplorer
{
	public class FileTreeBuilder
	{
		public delegate void FolderFoundHandler(object sender,FoundFolderEventArgs e);

		public delegate void FileFoundHandler(object sender,FoundFileEventArgs e);

		public delegate void EnumerationFinishedHandler(object sender,EnumerationFinishedEventArgs e);

		public event FolderFoundHandler FolderFound;
		public event FileFoundHandler FileFound;
		public event EnumerationFinishedHandler EnumerationFinished;

		private FoundFolderEventArgs foundFolderEventArgs = new FoundFolderEventArgs();
		private FoundFileEventArgs foundFileEventArgs = new FoundFileEventArgs();
		private EnumerationFinishedEventArgs enumerationFinishedEventArgs = new EnumerationFinishedEventArgs();

		List<string> listFile;

		public FileTreeBuilder()
		{
		}

		public void SetListfile(List<string> newListfile)
		{
			this.listFile = newListfile;
		}

		public void LoadDirectoryContentAsync(string parent = "")
		{
			/*Thread t = new Thread(() => EnumerateDirectories(parent));

			t.Start();*/

			EnumerateDirectories(parent);
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

						if (!String.IsNullOrEmpty(topDirectory) && !topLevelDirectories.Contains(topDirectory))
						{
							topLevelDirectories.Add(topDirectory);

							foundFolderEventArgs.folderRoot = parent;
							foundFolderEventArgs.folderName = topDirectory;

							OnFolderFound();
						}
						else if (String.IsNullOrEmpty(topDirectory) && slashIndex == -1 && IsFile(strippedPath))
						{
							// We've found a file!
							folderFileContent.Add(strippedPath);

							foundFileEventArgs.folderPath = parent;
							foundFileEventArgs.fileName = strippedPath;

							OnFileFound();
						}
					}
					else if (bHasFoundStartOfFolderBlock)
					{
						// We've read all the entries we need to, so abort
						break;
					}
				}

				enumerationFinishedEventArgs.parentPath = parent;
				enumerationFinishedEventArgs.topLevelDirectories = topLevelDirectories;
				enumerationFinishedEventArgs.folderFileContent = folderFileContent;

				OnEnumerationFinished();
			}
			else
			{
				// We're enumerating the top-level directories, so all paths are relevant
				List<string> topLevelDirectories = new List<string>();
				foreach (string path in listFile)
				{				
					int slashIndex = path.IndexOf('\\');
					string topDirectory = path.Substring(0, slashIndex + 1);

					if (!topLevelDirectories.Contains(topDirectory))
					{
						topLevelDirectories.Add(topDirectory);

						foundFolderEventArgs.folderRoot = parent;
						foundFolderEventArgs.folderName = topDirectory;

						OnFolderFound();
					}					
				}

				enumerationFinishedEventArgs.parentPath = "";
				enumerationFinishedEventArgs.topLevelDirectories = topLevelDirectories;
				enumerationFinishedEventArgs.folderFileContent = new List<string>();

				OnEnumerationFinished();
			}
		}

		private bool IsFile(string name)
		{
			string[] parts = name.Split('.');
			return parts.Length > 1;
		}

		private void OnFolderFound()
		{
			if (FolderFound != null)
			{
				FolderFound(this, foundFolderEventArgs);
			}
		}

		private void OnFileFound()
		{
			if (FileFound != null)
			{
				FileFound(this, foundFileEventArgs);
			}
		}

		private void OnEnumerationFinished()
		{	
			if (EnumerationFinished != null)
			{
				EnumerationFinished(this, enumerationFinishedEventArgs);
			}
		}
	}

	public class FoundFolderEventArgs : EventArgs
	{
		public string folderName;
		public string folderRoot;
	}

	public class FoundFileEventArgs : EventArgs
	{
		public string folderPath;
		public string fileName;
	}

	public class EnumerationFinishedEventArgs : EventArgs
	{
		public string parentPath;
		public List<string> topLevelDirectories;
		public List<string> folderFileContent;
	}
}

