using System;
using Gtk;

namespace MPQExplorer
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			Application.Init();
			MainWindow win = MainWindow.Create();
			win.Show();
			Application.Run();
		}
	}
}
