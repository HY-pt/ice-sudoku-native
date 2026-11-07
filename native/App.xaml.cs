using System;
using System.Windows;

namespace IceSudoku;

public partial class App : Application
{
    public static bool IsDark { get; private set; }

    public static void SetTheme(bool dark)
    {
        IsDark = dark;
        var uri = new Uri(dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative);
        var dicts = Current.Resources.MergedDictionaries;
        for (int i = 0; i < dicts.Count; i++)
        {
            if (dicts[i].Contains("AppBackgroundBrush"))
            {
                dicts[i] = new ResourceDictionary { Source = uri };
                break;
            }
        }
    }
}