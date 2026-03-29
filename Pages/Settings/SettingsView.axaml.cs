using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Settings;

public partial class SettingsView : UserControl
{
	public SettingsView()
	{
		InitializeComponent();
		DataContext = new SettingsViewModel();
	}

	private void BackButton_Click(object? sender, RoutedEventArgs e)
	{
		// 隐藏设置面板
		IsVisible = false;
	}

	private async void SelectFolder_Click(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not SettingsViewModel viewModel)
			return;

		var topLevel = TopLevel.GetTopLevel(this);
		if (topLevel == null)
			return;

		var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
			new FolderPickerOpenOptions { Title = "选择壁纸保存位置", AllowMultiple = false }
		);

		if (folders.Count > 0)
		{
			var folder = folders.First();
			if (folder.TryGetLocalPath() is { } path)
			{
				viewModel.SavePath = path;
			}
		}
	}

	private void OpenFolder_Click(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not SettingsViewModel viewModel)
			return;

		var path = viewModel.SavePath;
		if (System.IO.Directory.Exists(path))
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = $"\"{path}\"",
				UseShellExecute = true
			});
		}
	}
}
