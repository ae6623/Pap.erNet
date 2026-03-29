using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using Pap.erNet.Services;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

public class SettingsViewModel : ViewModelBase
{
	private bool _autoWallpaperEnabled;
	private string _selectedInterval = "5分钟";
	private string _savePath = @"H:\macos\壁纸\pap.er";
	private string _statusText = "未启动";

	public SettingsViewModel()
	{
		// 默认路径
		if (!System.IO.Directory.Exists(_savePath))
		{
			try
			{
				System.IO.Directory.CreateDirectory(_savePath);
			}
			catch
			{
				// 如果创建失败，使用临时文件夹
				_savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Pap.erNet", "Wallpapers");
				System.IO.Directory.CreateDirectory(_savePath);
			}
		}

		// 监听设置变化
		this.WhenAnyValue(x => x.AutoWallpaperEnabled).Subscribe(OnAutoWallpaperEnabledChanged);

		this.WhenAnyValue(x => x.SelectedInterval).Subscribe(OnIntervalChanged);

		this.WhenAnyValue(x => x.SavePath).Subscribe(OnSavePathChanged);
	}

	public bool AutoWallpaperEnabled
	{
		get => _autoWallpaperEnabled;
		set => this.RaiseAndSetIfChanged(ref _autoWallpaperEnabled, value);
	}

	public string SelectedInterval
	{
		get => _selectedInterval;
		set => this.RaiseAndSetIfChanged(ref _selectedInterval, value);
	}

	public string SavePath
	{
		get => _savePath;
		set => this.RaiseAndSetIfChanged(ref _savePath, value);
	}

	public string StatusText
	{
		get => _statusText;
		set => this.RaiseAndSetIfChanged(ref _statusText, value);
	}

	public List<string> IntervalOptions { get; } = new() { "1分钟", "5分钟", "10分钟", "15分钟", "30分钟", "1小时" };

	public int GetIntervalMinutes()
	{
		return SelectedInterval switch
		{
			"1分钟" => 1,
			"5分钟" => 5,
			"10分钟" => 10,
			"15分钟" => 15,
			"30分钟" => 30,
			"1小时" => 60,
			_ => 5,
		};
	}

	private void OnAutoWallpaperEnabledChanged(bool enabled)
	{
		if (enabled)
		{
			AutoWallpaperService.Instance.Start(SavePath, GetIntervalMinutes());
			StatusText = $"运行中 - 每 {SelectedInterval} 更换一次壁纸";
		}
		else
		{
			AutoWallpaperService.Instance.Stop();
			StatusText = "已停止";
		}
	}

	private void OnIntervalChanged(string interval)
	{
		if (AutoWallpaperEnabled)
		{
			AutoWallpaperService.Instance.Restart(SavePath, GetIntervalMinutes());
			StatusText = $"运行中 - 每 {interval} 更换一次壁纸";
		}
	}

	private void OnSavePathChanged(string path)
	{
		if (AutoWallpaperEnabled)
		{
			AutoWallpaperService.Instance.Restart(path, GetIntervalMinutes());
		}
	}
}
