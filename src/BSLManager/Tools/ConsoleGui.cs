using System.Reflection;
using Terminal.Gui;

namespace BSLManager.Tools;

public static class ConsoleGui
{
    public static void RefreshUI()
    {
        var bindingAttr = BindingFlags.Static | BindingFlags.NonPublic;
        var method = typeof(Application).GetMethod("TerminalResized", bindingAttr);
        method?.Invoke(null, null);
    }
}