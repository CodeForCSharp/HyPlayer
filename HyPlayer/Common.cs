﻿#region

using HyPlayer.Classes;
using HyPlayer.Controls;
using HyPlayer.HyPlayControl;
using HyPlayer.Pages;
using Kawazu;
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.UI.Xaml.Controls;
using NeteaseCloudMusicApi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

#if !DEBUG
using Microsoft.AppCenter.Crashes;
#endif

#endregion

namespace HyPlayer
{
    internal class Common
    {
        public static CloudMusicApi ncapi = new CloudMusicApi();
        public static bool Logined = false;
        public static NCUser LoginedUser;
        public static ExpandedPlayer PageExpandedPlayer;
        public static MainPage PageMain;
        public static PlayBar BarPlayBar;
        public static Frame BaseFrame;
        public static BasePage PageBase;
        public static Setting Setting = new Setting();
        public static bool ShowLyricSound = true;
        public static bool ShowLyricTrans = true;
        public static Dictionary<string, object> GLOBAL = new Dictionary<string, object>();
        public static List<string> LikedSongs = new List<string>();
        public static KawazuConverter KawazuConv;
        public static List<NCPlayList> MySongLists = new List<NCPlayList>();
        public static readonly Stack<NavigationHistoryItem> NavigationHistory = new Stack<NavigationHistoryItem>();
        public static bool isExpanded = false;
        public static TeachingTip GlobalTip;

        public static bool NavigatingBack;

        public static async void Invoke(Action action, CoreDispatcherPriority Priority = CoreDispatcherPriority.Normal)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Priority,
                    () => { action(); });
            }
#if DEBUG
            catch
            {
#else
            catch (Exception e)
            {
                Crashes.TrackError(e);
#endif

                /*
                Invoke((async () =>
                {
                    await new ContentDialog
                    {
                        Title = "发生错误",
                        Content = "Error: " + e.Message + "\r\n" + e.StackTrace,
                        CloseButtonText = "关闭",
                        DefaultButton = ContentDialogButton.Close
                    }.ShowAsync();
                }));
                */
            }
        }


        public static void ShowTeachingTip(string title, string subtitle = "")
        {
            Invoke(() =>
            {
                GlobalTip.Title = title;
                GlobalTip.Subtitle = subtitle ?? "";
                if (!GlobalTip.IsOpen)
                {
                    GlobalTip.IsOpen = true;
                }
                else
                {
                    GlobalTip.IsOpen = false;
                    GlobalTip.IsOpen = true;
                }
            });
        }

        public static void NavigatePage(Type SourcePageType, object paratmer = null, object ignore = null)
        {
            if (Common.Setting.forceMemoryGarbage)
            {
                if (NavigationHistory.Count >= 1 && PageBase.NavMain.SelectedItem == NavigationHistory.Peek().Item)
                    PageBase.NavMain.SelectedItem = PageBase.ItemMain;
                NavigationHistory.Push(new NavigationHistoryItem
                {
                    PageType = SourcePageType,
                    Paratmers = paratmer,
                    Item = PageBase.NavMain.SelectedItem
                });
                BaseFrame?.Navigate(SourcePageType, paratmer);
                GC.Collect();
            }
            else
            {
                BaseFrame?.Navigate(SourcePageType, paratmer);
            }
        }

        public static void NavigateRefresh()
        {
            var peek = NavigationHistory.Peek();
            BaseFrame?.Navigate(peek.PageType, peek.Paratmers);
            GC.Collect();
        }

        public static void NavigatePageResource(string resourceId)
        {
            switch (resourceId.Substring(0, 2))
            {
                case "al":
                    NavigatePage(typeof(AlbumPage), resourceId.Substring(2));
                    break;
                case "pl":
                    NavigatePage(typeof(SongListDetail), resourceId.Substring(2));
                    break;
                case "rd":
                    NavigatePage(typeof(RadioPage), resourceId.Substring(2));
                    break;
                case "ar":
                    NavigatePage(typeof(ArtistPage), resourceId.Substring(2));
                    break;
                case "us":
                    NavigatePage(typeof(Me), resourceId.Substring(2));
                    break;
                case "ns":
                    Invoke(async () =>
                    {
                        await HyPlayList.AppendNcSource(resourceId);
                        HyPlayList.SongAppendDone();
                        HyPlayList.SongMoveTo(HyPlayList.List.FindIndex(t => "ns" + t.PlayItem.Id == resourceId));
                    });

                    break;
            }
        }

        public static void CollectGarbage()
        {
            NavigatePage(typeof(BlankPage));
            BaseFrame.Content = null;
            PageExpandedPlayer?.Dispose();
            PageExpandedPlayer = null;
            PageMain.ExpandedPlayer.Navigate(typeof(BlankPage));
            _ = ImageCache.Instance.ClearAsync();
            KawazuConv = null;
        }

        public static void UIElement_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var element = sender as UIElement;
            try
            {
                element.ContextFlyout.ShowAt(element, new FlyoutShowOptions { Position = e.GetPosition(element) });
            }
            catch
            {
                var flyout = FlyoutBase.GetAttachedFlyout((FrameworkElement)element);
                flyout.ShowAt(element, new FlyoutShowOptions { Position = e.GetPosition(element) });
            }
        }

        public static void NavigateBack()
        {
            if (Common.Setting.forceMemoryGarbage)
            {
                if (NavigationHistory.Count > 1)
                    NavigationHistory.Pop();
                try
                {
                    var bak = NavigationHistory.Peek();
                    while (bak.PageType == typeof(BlankPage))
                    {
                        NavigationHistory.Pop();
                        bak = NavigationHistory.Peek();
                    }

                    BaseFrame?.Navigate(bak.PageType, bak.Paratmers);
                    NavigatingBack = true;
                    PageBase.NavMain.SelectedItem = bak.Item;
                    NavigatingBack = false;
                    GC.Collect();
                }
                catch
                {
                }
            }
            else
            {
                if (BaseFrame != null && BaseFrame.CanGoBack)
                    BaseFrame?.GoBack();
            }

        }

        public class NavigationHistoryItem
        {
            public object Item;
            public Type PageType;
            public object Paratmers;
        }
    }

    internal class ColorHelper
    {
        public static Color GetReversedColor(Color color)
        {
            var grayLevel = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            if (grayLevel > 0.1)
                return Colors.Black;
            return Colors.White;
        }
    }

    internal class Setting : INotifyPropertyChanged
    {
        public int lyricSize
        {
            get
            {
                var ret = 0;
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("lyricSize"))
                    if (int.TryParse(ApplicationData.Current.LocalSettings.Values["lyricSize"].ToString(), out ret))
                        return ret;
                return ret;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["lyricSize"] = value;
                OnPropertyChanged();
            }
        }

        public bool forceMemoryGarbage
        {
            get => GetSettings("forceMemoryGarbage", false);
            set => ApplicationData.Current.LocalSettings.Values["forceMemoryGarbage"] = value;
        }

        public bool lyricDropshadow
        {
            get => GetSettings("lyricDropshadow", false);
            set => ApplicationData.Current.LocalSettings.Values["lyricDropshadow"] = value;
        }

        public bool safeFileAccess
        {
            get => GetSettings("safeFileAccess", false);
            set => ApplicationData.Current.LocalSettings.Values["safeFileAccess"] = value;
        }

        public List<string> scanLocalFolder
        {
            get
            {
                var folders = GetSettings("scanLocalFolder", KnownFolders.MusicLibrary.Path);
                return folders.Split("\r\n").ToList();
            }
            set => ApplicationData.Current.LocalSettings.Values["safeFileAccess"] = string.Join("\r\n", value);
        }

        public int lyricColor
        {
            get => GetSettings("lyricColor", 0);
            set => ApplicationData.Current.LocalSettings.Values["lyricColor"] = value;
        }

        public bool albumRotate
        {
            get => GetSettings("albumRotate", false);
            set => ApplicationData.Current.LocalSettings.Values["albumRotate"] = value;
        }

        public bool albumRound
        {
            get => GetSettings("albumRound", false);
            set => ApplicationData.Current.LocalSettings.Values["albumRound"] = value;
        }

        public int albumBorderLength
        {
            get => GetSettings("albumBorderLength", 0);
            set => ApplicationData.Current.LocalSettings.Values["albumBorderLength"] = value;
        }

        public int romajiSize
        {
            get => GetSettings("romajiSize", 15);
            set
            {
                ApplicationData.Current.LocalSettings.Values["romajiSize"] = value;
                OnPropertyChanged();
            }
        }

        public bool lyricAlignment
        {
            get => GetSettings("lyricAlignment", false);
            set
            {
                ApplicationData.Current.LocalSettings.Values["lyricAlignment"] = value;
                OnPropertyChanged();
            }
        }

        public bool ancientSMTC
        {
            get => GetSettings("ancientSMTC", false);
            set
            {
                ApplicationData.Current.LocalSettings.Values["ancientSMTC"] = value;
                OnPropertyChanged();
            }
        }

        public string audioRate
        {
            get => GetSettings("audioRate", "320000");
            set
            {
                ApplicationData.Current.LocalSettings.Values["audioRate"] = value;
                OnPropertyChanged();
            }
        }

        public string downloadAudioRate
        {
            get => GetSettings("downloadAudioRate", "999000");
            set
            {
                ApplicationData.Current.LocalSettings.Values["downloadAudioRate"] = value;
                OnPropertyChanged();
            }
        }

        public bool xboxHidePointer
        {
            get => GetSettings("xboxHidePointer", false);
            set => ApplicationData.Current.LocalSettings.Values["xboxHidePointer"] = value;
        }
        public int Volume
        {
            get
            {
                try
                {
                    return GetSettings("Volume", 50);
                }
                catch
                {
                    return 50;
                }
            }

            set => ApplicationData.Current.LocalSettings.Values["Volume"] = value;
        }

        public string downloadDir
        {
            get
            {
                try
                {
                    return GetSettings("downloadDir", KnownFolders.MusicLibrary
                        .CreateFolderAsync("HyPlayer", CreationCollisionOption.OpenIfExists).AsTask().Result.Path);
                }
                catch
                {
                    return ApplicationData.Current.LocalCacheFolder.Path;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["downloadDir"] = value;
                OnPropertyChanged();
            }
        }

        public string cacheDir
        {
            get
            {
                try
                {
                    return GetSettings("cacheDir", ApplicationData.Current.LocalCacheFolder
                        .CreateFolderAsync("songCache", CreationCollisionOption.OpenIfExists).AsTask().GetAwaiter()
                        .GetResult().Path);
                }
                catch
                {
                    return ApplicationData.Current.LocalCacheFolder.Path;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["cacheDir"] = value;
                OnPropertyChanged();
            }
        }

        public bool fadeInOut
        {
            get => GetSettings("FadeInOut", false);
            set
            {
                ApplicationData.Current.LocalSettings.Values["FadeInOut"] = value;
                OnPropertyChanged();
            }
        }

        public bool fadeInOutPause
        {
            get => GetSettings("FadeInOutPause", false);
            set
            {
                ApplicationData.Current.LocalSettings.Values["FadeInOutPause"] = value;
                OnPropertyChanged();
            }
        }

        public bool notClearMode
        {
            get => GetSettings("notClearMode", true);
            set
            {
                ApplicationData.Current.LocalSettings.Values["notClearMode"] = value;
                OnPropertyChanged();
            }
        }

        public double fadeInOutTime
        {
            get
            {
                try
                {
                    return GetSettings<double>("fadeInOutTime", 3);
                }
                catch
                {
                    return 3;
                }
            }

            set => ApplicationData.Current.LocalSettings.Values["fadeInOutTime"] = value;
        }

        public double fadeInOutTimePause
        {
            get
            {
                try
                {
                    return GetSettings<double>("fadeInOutTimePause", 3);
                }
                catch
                {
                    return 3;
                }
            }

            set => ApplicationData.Current.LocalSettings.Values["fadeInOutTimePause"] = value;
        }

        public bool toastLyric
        {
            get => GetSettings("toastLyric", false);
            set
            {
                ApplicationData.Current.LocalSettings.Values["toastLyric"] = value;
                OnPropertyChanged();
            }
        }

        public bool expandAnimation
        {
            get => GetSettings("expandAnimation", true);
            set
            {
                ApplicationData.Current.LocalSettings.Values["expandAnimation"] = value ? "true" : "false";
                OnPropertyChanged();
            }
        }

        public bool uiSound
        {
            get => GetSettings("uiSound", false);
            set
            {
                ApplicationData.Current.LocalSettings.Values["uiSound"] = value;
                OnPropertyChanged();
            }
        }

        public int songRollType
        {
            get => GetSettings("songRollType", 0);
            set
            {
                ApplicationData.Current.LocalSettings.Values["songRollType"] = value;
                OnPropertyChanged();
            }
        }

        public bool songUrlLazyGet
        {
            get => GetSettings("songUrlLazyGet", true);
            set
            {
                ApplicationData.Current.LocalSettings.Values["songUrlLazyGet"] = value;
                OnPropertyChanged();
            }
        }

        public bool enableCache
        {
            get => GetSettings("enableCache", false);
            set
            {
                ApplicationData.Current.LocalSettings.Values["enableCache"] = value;
                OnPropertyChanged();
            }
        }

        public bool highQualityCoverInSMTC
        {
            get => GetSettings("highQualityCoverInSMTC", false);
            set
            {
                ApplicationData.Current.LocalSettings.Values["highQualityCoverInSMTC"] = value;
                OnPropertyChanged();
            }
        }

        public int themeRequest
        {
            // 0 - 未设置   1 - 浅色  2 - 深色
            get => GetSettings("themeRequest", 0);
            set
            {
                ApplicationData.Current.LocalSettings.Values["themeRequest"] = value;
                OnPropertyChanged();
            }
        }

        public int expandedCoverShadowDepth
        {
            get => GetSettings("expandedCoverShadowDepth", 4);
            set
            {
                ApplicationData.Current.LocalSettings.Values["expandedCoverShadowDepth"] = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public async void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => { PropertyChanged(this, new PropertyChangedEventArgs(propertyName)); });
        }

        public static T GetSettings<T>(string propertyName, T defaultValue)
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(propertyName) &&
                    ApplicationData.Current.LocalSettings.Values[propertyName] != null &&
                    !string.IsNullOrEmpty(ApplicationData.Current.LocalSettings.Values[propertyName].ToString()))
                {
                    if (typeof(T).ToString() == "System.Boolean")
                        return (T)(object)bool.Parse(ApplicationData.Current.LocalSettings.Values[propertyName]
                            .ToString());

                    //超长的IF
                    return (T)ApplicationData.Current.LocalSettings.Values[propertyName];
                }

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
    }

    internal class HistoryManagement
    {
        public static void InitializeHistoryTrack()
        {
            var list = new List<string>();
            if (ApplicationData.Current.LocalSettings.Values["songHistory"] == null)
                ApplicationData.Current.LocalSettings.Values["songHistory"] = JsonConvert.SerializeObject(list);
            if (ApplicationData.Current.LocalSettings.Values["songHistory"].ToString().StartsWith("[{"))
                ApplicationData.Current.LocalSettings.Values["songHistory"] = JsonConvert.SerializeObject(list);
            if (ApplicationData.Current.LocalSettings.Values["searchHistory"] == null)
                ApplicationData.Current.LocalSettings.Values["searchHistory"] = JsonConvert.SerializeObject(list);
            if (ApplicationData.Current.LocalSettings.Values["songlistHistory"] == null)
                ApplicationData.Current.LocalSettings.Values["songlistHistory"] = JsonConvert.SerializeObject(list);
            if (ApplicationData.Current.LocalSettings.Values["curPlayingListHistory"] == null)
                ApplicationData.Current.LocalSettings.Values["curPlayingListHistory"] =
                    JsonConvert.SerializeObject(list);
            if (ApplicationData.Current.LocalSettings.Values["curPlayingListHistory"].ToString().StartsWith("[{"))
                ApplicationData.Current.LocalSettings.Values["curPlayingListHistory"] =
                    JsonConvert.SerializeObject(list);
            if (ApplicationData.Current.LocalSettings.Values["songlistHistory"].ToString().StartsWith("[{"))
                ApplicationData.Current.LocalSettings.Values["songlistHistory"] = JsonConvert.SerializeObject(list);
        }

        public static void AddNCSongHistory(string songid)
        {
            var list = new List<string>();
            list = JsonConvert.DeserializeObject<List<string>>(ApplicationData.Current.LocalSettings
                .Values["songHistory"].ToString());

            list.Remove(songid);
            list.Insert(0, songid);
            if (list.Count >= 300)
                list.RemoveRange(9, list.Count - 300);
            ApplicationData.Current.LocalSettings.Values["songHistory"] = JsonConvert.SerializeObject(list);
        }

        public static void AddSearchHistory(string Text)
        {
            var list = new List<string>();
            list = JsonConvert.DeserializeObject<List<string>>(ApplicationData.Current.LocalSettings
                .Values["searchHistory"].ToString());
            if (!list.Contains(Text))
            {
                list.Insert(0, Text);
            }
            else
            {
                list.RemoveAll(t => t == Text);
                list.Insert(0, Text);
            }

            ApplicationData.Current.LocalSettings.Values["searchHistory"] = JsonConvert.SerializeObject(list);
        }

        public static void AddSonglistHistory(string playListid)
        {
            var list = new List<string>();
            list = JsonConvert.DeserializeObject<List<string>>(ApplicationData.Current.LocalSettings
                .Values["songlistHistory"].ToString());

            list.Remove(playListid);
            list.Insert(0, playListid);
            if (list.Count >= 100)
                list.RemoveRange(100, list.Count - 100);
            ApplicationData.Current.LocalSettings.Values["songlistHistory"] = JsonConvert.SerializeObject(list);
        }

        public static void SetcurPlayingListHistory(List<string> songids)
        {
            //现在暂存100首,之后引入高级数据库会多加点
            ApplicationData.Current.LocalSettings.Values["curPlayingListHistory"] =
                JsonConvert.SerializeObject(songids.Count > 100 ? songids.GetRange(0, 100) : songids);
        }

        public static void ClearHistory()
        {
            var list = new List<string>();
            ApplicationData.Current.LocalSettings.Values["songlistHistory"] = JsonConvert.SerializeObject(list);
            ApplicationData.Current.LocalSettings.Values["songHistory"] = JsonConvert.SerializeObject(list);
            ApplicationData.Current.LocalSettings.Values["searchHistory"] = JsonConvert.SerializeObject(list);
        }

        public static async Task<List<NCSong>> GetNCSongHistory()
        {
            var retsongs = new List<NCSong>();
            try
            {
                var json = await Common.ncapi.RequestAsync(CloudMusicApiProviders.SongDetail,
                    new Dictionary<string, object>
                    {
                        ["ids"] = string.Join(",",
                            JsonConvert.DeserializeObject<List<string>>(ApplicationData.Current.LocalSettings
                                .Values["songHistory"].ToString()))
                    });
                return json["songs"].ToArray().Select(t => NCSong.CreateFromJson(t)).ToList();
            }
            catch (Exception e)
            {
                Common.ShowTeachingTip(e.Message, (e.InnerException ?? new Exception()).Message);
            }

            return new List<NCSong>();
        }

        public static async Task<List<NCPlayList>> GetSonglistHistory()
        {
            var i = 0;
            var queries = new Dictionary<string, object>();
            foreach (var plid in JsonConvert.DeserializeObject<List<string>>(ApplicationData.Current.LocalSettings
                .Values["songlistHistory"].ToString()))
                queries["/api/v6/playlist/detail" + new string('/', i++)] = JsonConvert.SerializeObject(
                    new Dictionary<string, object>
                    {
                        ["id"] = plid,
                        ["n"] = 100000,
                        ["s"] = 8
                    });
            if (queries.Count == 0) return new List<NCPlayList>();
            var ret = new List<NCPlayList>();
            try
            {
                var json = await Common.ncapi.RequestAsync(CloudMusicApiProviders.Batch, queries);

                for (var k = 0; k < json.Count - 1; k++)
                    ret.Add(NCPlayList.CreateFromJson(
                        json["/api/v6/playlist/detail" + new string('/', k)]["playlist"]));
            }
            catch (Exception e)
            {
                Common.ShowTeachingTip(e.Message, (e.InnerException ?? new Exception()).Message);
            }

            return ret;
        }

        public static List<string> GetSearchHistory()
        {
            return JsonConvert.DeserializeObject<List<string>>(ApplicationData.Current.LocalSettings
                .Values["searchHistory"].ToString());
        }

        public static async Task<List<NCSong>> GetcurPlayingListHistory()
        {
            var retsongs = new List<NCSong>();
            var hisSongs = JsonConvert.DeserializeObject<List<string>>(ApplicationData.Current.LocalSettings
                .Values["curPlayingListHistory"].ToString());
            if (hisSongs == null || hisSongs.Count == 0)
                return retsongs;
            try
            {
                var json = await Common.ncapi.RequestAsync(CloudMusicApiProviders.SongDetail,
                    new Dictionary<string, object>
                    {
                        ["ids"] = string.Join(",", hisSongs)
                    });
                return json["songs"].ToArray().Select(t => NCSong.CreateFromJson(t)).ToList();
            }
            catch (Exception e)
            {
                Common.ShowTeachingTip(e.Message, (e.InnerException ?? new Exception()).Message);
            }

            return new List<NCSong>();
        }
    }

    internal static class Extensions
    {
        public static byte[] ToByteArrayUtf8(this string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        public static string ToHexStringLower(this byte[] value)
        {
            var sb = new StringBuilder();
            foreach (var t in value) sb.Append(t.ToString("x2"));

            return sb.ToString();
        }

        public static string ToHexStringUpper(this byte[] value)
        {
            var sb = new StringBuilder();
            foreach (var t in value) sb.Append(t.ToString("X2"));

            return sb.ToString();
        }

        public static string ToBase64String(this byte[] value)
        {
            return Convert.ToBase64String(value);
        }

        public static byte[] ComputeMd5(this byte[] value)
        {
            var md5 = MD5.Create();
            return md5.ComputeHash(value);
        }

        public static byte[] RandomBytes(this Random random, int length)
        {
            var buffer = new byte[length];
            random.NextBytes(buffer);
            return buffer;
        }

        public static string Get(this CookieCollection cookies, string name, string defaultValue)
        {
            return cookies[name]?.Value ?? defaultValue;
        }
    }
}