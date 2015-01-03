using System;
using System.Collections.Generic;
using System.Linq;
using PoeHUD.Controllers;
using PoeHUD.Framework;
using PoeHUD.Hud.UI;

using SharpDX;
using SharpDX.Direct3D9;

namespace PoeHUD.Hud.Preload
{
    public class PreloadAlert : Plugin
    {
        private readonly HashSet<string> disp;
        private Dictionary<string, string> alertStrings;
        private int lastCount;

        public PreloadAlert(GameController gameController, Graphics graphics) : base(gameController, graphics)
        {
            disp = new HashSet<string>();
            InitAlertStrings();
            GameController.Area.OnAreaChange += CurrentArea_OnAreaChange;
            CurrentArea_OnAreaChange(GameController.Area);
        }


        private void CurrentArea_OnAreaChange(AreaController area)
        {
            if (Settings.GetBool("PreloadAlert"))
            {
                Parse();
            }
        }

        private void Parse()
        {
            disp.Clear();
            int pFileRoot = GameController.Memory.ReadInt(GameController.Memory.AddressOfProcess + GameController.Memory.offsets.FileRoot);
            int num2 = GameController.Memory.ReadInt(pFileRoot + 12);
            int listIterator = GameController.Memory.ReadInt(pFileRoot + 20);
            int areaChangeCount = GameController.Game.AreaChangeCount;
            for (int i = 0; i < num2; i++)
            {
                listIterator = GameController.Memory.ReadInt(listIterator);
                if (GameController.Memory.ReadInt(listIterator + 8) != 0 && GameController.Memory.ReadInt(listIterator + 12, 36) == areaChangeCount)
                {
                    string text = GameController.Memory.ReadStringU(GameController.Memory.ReadInt(listIterator + 8));
                    if (text.Contains("vaal_sidearea"))
                    {
                        disp.Add("Area contains Corrupted Area");
                    }
                    if (text.Contains('@'))
                    {
                        text = text.Split(new[] {'@'})[0];
                    }
                    if (text.StartsWith("Metadata/Monsters/Missions/MasterStrDex"))
                    {
                        Console.WriteLine("bad alert " + text);
                        disp.Add("Area contains Vagan, Weaponmaster");
                    }
                    if (alertStrings.ContainsKey(text))
                    {
                        Console.WriteLine("Alert because of " + text);
                        disp.Add(alertStrings[text]);
                    }
                    else
                    {
                        if (text.EndsWith("BossInvasion"))
                        {
                            disp.Add("Area contains Invasion Boss");
                        }
                    }
                }
            }
        }

        public override void Render(Dictionary<UiMountPoint, Vector2> mountPoints)
        {
            if (!Settings.GetBool("PreloadAlert"))
            {
                return;
            }
            int num =
                GameController.Memory.ReadInt(
                    GameController.Memory.AddressOfProcess + GameController.Memory.offsets.FileRoot, 12);
            if (num != lastCount)
            {
                lastCount = num;
                Parse();
            }
            if (disp.Count > 0)
            {
                var vec = mountPoints[UiMountPoint.LeftOfMinimap];
                float num2 = vec.Y;
                int maxWidth = 0;
                int @int = Settings.GetInt("PreloadAlert.FontSize");
                int int2 = Settings.GetInt("PreloadAlert.BgAlpha");
                foreach (string current in disp)
                {
                    var vec2 = Graphics.DrawText(current, @int, new Vector2(vec.X, num2), Color.White, FontDrawFlags.Right);
                    if (vec2.Width + 10 > maxWidth)
                    {
                        maxWidth = vec2.Width + 10;
                    }
                    num2 += vec2.Height;
                }
                if (maxWidth > 0 && int2 > 0)
                {
                    var bounds = new RectangleF(vec.X - maxWidth + 5, vec.Y - 5, maxWidth, num2 - vec.Y + 10);
                    Graphics.DrawBox(bounds, new ColorBGRA(1, 1, 1, (byte)int2));
                    mountPoints[UiMountPoint.LeftOfMinimap] = new Vector2(vec.X, vec.Y + 5 + bounds.Height);
                }
            }
        }

        private void InitAlertStrings()
        {
            alertStrings = LoadConfig("config/preload_alerts.txt");
        }
    }
}