using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Pap.erNet.Models;
using Pap.erNet.Utils;

namespace Pap.erNet.Services;

public class AutoWallpaperService
{
	private static AutoWallpaperService? _instance;
	private static readonly object _lock = new();

	private System.Timers.Timer? _timer;
	private string _savePath = "";
	private int _intervalMinutes = 5;
	private bool _isRunning = false;
	private CancellationTokenSource? _cancellationTokenSource;

	public static AutoWallpaperService Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_lock)
				{
					_instance ??= new AutoWallpaperService();
				}
			}
			return _instance;
		}
	}

	private AutoWallpaperService() { }

	public void Start(string savePath, int intervalMinutes)
	{
		if (_isRunning)
		{
			Stop();
		}

		_savePath = savePath;
		_intervalMinutes = intervalMinutes;
		_isRunning = true;
		_cancellationTokenSource = new CancellationTokenSource();

		// 确保保存路径存在
		if (!Directory.Exists(_savePath))
		{
			Directory.CreateDirectory(_savePath);
		}

		// 立即执行一次
		_ = ExecuteAsync(_cancellationTokenSource.Token);

		// 设置定时器
		_timer = new System.Timers.Timer(intervalMinutes * 60 * 1000);
		_timer.Elapsed += async (s, e) => await ExecuteAsync(_cancellationTokenSource.Token);
		_timer.AutoReset = true;
		_timer.Start();
	}

	public void Stop()
	{
		_isRunning = false;
		_timer?.Stop();
		_timer?.Dispose();
		_timer = null;
		_cancellationTokenSource?.Cancel();
		_cancellationTokenSource?.Dispose();
		_cancellationTokenSource = null;
	}

	public void Restart(string savePath, int intervalMinutes)
	{
		Stop();
		Start(savePath, intervalMinutes);
	}

	private async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (cancellationToken.IsCancellationRequested)
				return;

			// 获取一张随机壁纸
			var wallpaper = await GetRandomWallpaperAsync();
			if (wallpaper == null)
				return;

			if (cancellationToken.IsCancellationRequested)
				return;

			// 下载壁纸
			var filePath = await DownloadWallpaperAsync(wallpaper, cancellationToken);
			if (string.IsNullOrEmpty(filePath))
				return;

			if (cancellationToken.IsCancellationRequested)
				return;

			// 设置为桌面壁纸
			SetDesktopWallpaper(filePath);

			LogHelper.WriteLogAsync($"自动更换壁纸: {wallpaper.Author} - {filePath}");
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"自动更换壁纸失败: {ex.Message}");
		}
	}

	private async Task<Wallpaper?> GetRandomWallpaperAsync()
	{
		try
		{
			var service = new WallpaperListService();
			var wallpapers = new System.Collections.Generic.List<Wallpaper>();

			// 从发现列表获取壁纸
			await foreach (var wallpaper in service.DiscoverItemsAsync())
			{
				wallpapers.Add(wallpaper);
				if (wallpapers.Count >= 10) // 获取10张
					break;
			}

			if (wallpapers.Count == 0)
				return null;

			// 随机选择一张
			var random = new Random();
			return wallpapers[random.Next(wallpapers.Count)];
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"获取壁纸列表失败: {ex.Message}");
			return null;
		}
	}

	private async Task<string?> DownloadWallpaperAsync(Wallpaper wallpaper, CancellationToken cancellationToken)
	{
		try
		{
			// 获取高清图片URL (将 thumb 替换为原始图片)
			var imageUrl = wallpaper.Url.Replace("thumb", "raw");

			// 生成文件名
			var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{wallpaper.Id}.jpg";
			var filePath = Path.Combine(_savePath, fileName);

			// 下载图片
			using var client = new System.Net.Http.HttpClient();
			client.Timeout = TimeSpan.FromMinutes(2);

			var response = await client.GetAsync(imageUrl, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				// 如果 raw 失败，尝试下载 thumb
				response = await client.GetAsync(wallpaper.Url, cancellationToken);
				if (!response.IsSuccessStatusCode)
					return null;
			}

			var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
			await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);

			// 清理旧文件（保留最近50张）
			CleanupOldWallpapers();

			return filePath;
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"下载壁纸失败: {ex.Message}");
			return null;
		}
	}

	private void CleanupOldWallpapers()
	{
		try
		{
			var files = Directory
				.GetFiles(_savePath, "*.jpg")
				.Select(f => new FileInfo(f))
				.OrderByDescending(f => f.CreationTime)
				.Skip(50)
				.ToList();

			foreach (var file in files)
			{
				try
				{
					file.Delete();
				}
				catch { }
			}
		}
		catch { }
	}

	#region Windows 壁纸设置

	private const int SPI_SETDESKWALLPAPER = 20;
	private const int SPIF_UPDATEINIFILE = 0x01;
	private const int SPIF_SENDWININICHANGE = 0x02;

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

	private void SetDesktopWallpaper(string filePath)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, filePath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
		}
	}

	#endregion
}
