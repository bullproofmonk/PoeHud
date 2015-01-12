using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PoeHUD.Controllers;
using PoeHUD.Framework.Helpers;
using PoeHUD.Hud.Interfaces;
using PoeHUD.Hud.Settings;
using PoeHUD.Hud.UI;
using PoeHUD.Models;
using PoeHUD.Models.Enums;
using PoeHUD.Models.Interfaces;
using PoeHUD.Poe.Components;
using PoeHUD.Poe.Elements;
using PoeHUD.Poe.UI.Elements;

using SharpDX;
using SharpDX.Direct3D9;

using Entity = PoeHUD.Poe.Entity;
using Map = PoeHUD.Poe.Components.Map;

namespace PoeHUD.Hud.Loot
{
    public class ItemAlertPlugin : SizedPluginWithMapIcons <ItemAlertSettings>
	{
		private HashSet<long> playedSoundsCache;
		private Dictionary<EntityWrapper, AlertDrawStyle> currentAlerts;
		private Dictionary<int, ItemsOnGroundLabelElement> currentLabels;
		private Dictionary<string, CraftingBase> craftingBases;
		private HashSet<string> currencyNames;

	    public ItemAlertPlugin(GameController gameController, Graphics graphics, ItemAlertSettings settings)
            : base(gameController, graphics, settings)
	    {
            playedSoundsCache = new HashSet<long>();
            currentAlerts = new Dictionary<EntityWrapper, AlertDrawStyle>();
            currentLabels=new Dictionary<int, ItemsOnGroundLabelElement>();
            currencyNames = LoadCurrency();
            craftingBases = LoadCraftingBases();

            GameController.Area.OnAreaChange += CurrentArea_OnAreaChange;
	    }

	    public override void Dispose()
	    {
            GameController.Area.OnAreaChange -= CurrentArea_OnAreaChange;
	    }

	    
		protected override void OnEntityRemoved(EntityWrapper entity)
		{
            base.OnEntityRemoved(entity);
			currentAlerts.Remove(entity);
			currentLabels.Remove(entity.Address);
		}

        protected override void OnEntityAdded(EntityWrapper entity)
		{
			if (!Settings.Enable || currentAlerts.ContainsKey(entity))
			{
				return;
			}
			if (entity.HasComponent<WorldItem>())
			{
			    IEntity item = entity.GetComponent<WorldItem>().ItemEntity;
				ItemUsefulProperties props = EvaluateItem(item);

                if (props.IsWorthAlertingPlayer(currencyNames, Settings))
				{
					AlertDrawStyle drawStyle = props.GetDrawStyle();
					currentAlerts.Add(entity, drawStyle);
					CurrentIcons[entity] = new MapIcon(entity, new HudTexture("minimap_default_icon.png", drawStyle.color), () => Settings.ShowItemOnMap, 8);

					if (Settings.PlaySound && !playedSoundsCache.Contains(entity.LongId))
					{
						playedSoundsCache.Add(entity.LongId);
						Sounds.AlertSound.Play();
					}
				}
			}
		}


		private ItemUsefulProperties EvaluateItem(IEntity item)
		{
			ItemUsefulProperties ip = new ItemUsefulProperties();

			Mods mods = item.GetComponent<Mods>();
			Sockets socks = item.GetComponent<Sockets>();
			Map map = item.HasComponent<Map>() ? item.GetComponent<Map>() : null;
			SkillGem sk = item.HasComponent<SkillGem>() ? item.GetComponent<SkillGem>() : null;
			Quality q = item.HasComponent<Quality>() ? item.GetComponent<Quality>() : null;

			ip.Name = GameController.Files.BaseItemTypes.Translate(item.Path);
			ip.ItemLevel = mods.ItemLevel;
			ip.NumLinks = socks.LargestLinkSize;
			ip.NumSockets = socks.NumberOfSockets;
			ip.Rarity = mods.ItemRarity;
			ip.MapLevel = map == null ? 0 : 1;
			ip.IsCurrency = item.Path.Contains("Currency");
			ip.IsSkillGem = sk != null;
			ip.Quality = q == null ? 0 : q.ItemQuality;
			ip.WorthChrome = socks != null && socks.IsRGB;

		    ip.IsWeapon = item.HasComponent<Weapon>();
		    ip.IsArmour = item.HasComponent<Armour>();
		    ip.IsFlask = item.HasComponent<Flask>();

			ip.IsVaalFragment = item.Path.Contains("VaalFragment");

			CraftingBase craftingBase;
			if (craftingBases.TryGetValue(ip.Name, out craftingBase) && Settings.Crafting)
				ip.IsCraftingBase = ip.ItemLevel >= craftingBase.MinItemLevel 
					&& ip.Quality >= craftingBase.MinQuality
					&& (craftingBase.Rarities == null || craftingBase.Rarities.Contains(ip.Rarity));

			return ip;
		}

		private void CurrentArea_OnAreaChange(AreaController area)
		{
			playedSoundsCache.Clear();
			currentLabels.Clear();
		}
		public override void Render()
		{
            base.Render();
			if (!Settings.Enable || !Settings.ShowText)
			{
				return;
			}


            var playerPos = GameController.Player.GetComponent<Positioned>().GridPos;

		    var rightTopAnchor = StartDrawPointFunc();
			float y = rightTopAnchor.Y;
            const int vMargin = 2;
			foreach (KeyValuePair<EntityWrapper, AlertDrawStyle> kv in currentAlerts)
			{
				if (!kv.Key.IsValid) continue;

				string text = GetItemName(kv);
				if( null == text ) continue;

			    if (Settings.BorderSettings.Enable)
			    {
			        DrawBorder(kv.Key.Address);
			    }
			    Vector2 itemPos = kv.Key.GetComponent<Positioned>().GridPos;
				var delta = itemPos - playerPos;

				var vPadding = new Vector2(5, 2);
                var itemDrawnSize = DrawItem(kv.Value, delta, rightTopAnchor.X, y, vPadding, text);
				y += itemDrawnSize.Y + vMargin;
			}
			Size=new Size2F(0,y); //bug absent width
		}

        private void DrawBorder(int entityAddres)
        {
            if (currentLabels.ContainsKey(entityAddres))
            {
                var entitylabel = currentLabels[entityAddres];
                if (entitylabel.IsVisible)
                {
                    var rect = entitylabel.Label.GetClientRect();
                    var ui = GameController.Game.IngameState.IngameUi;
                    if (ui.OpenLeftPanel.IsVisible && ui.OpenLeftPanel.GetClientRect().Intersects(rect)
                        || ui.OpenRightPanel.IsVisible && ui.OpenRightPanel.GetClientRect().Intersects(rect))
                    {
                        return;
                    }

                    ColorNode borderColor = Settings.BorderSettings.BorderColor;
                    if (!entitylabel.CanPickUp)
                    {
                        borderColor = Settings.BorderSettings.CantPickUpBorderColor;
                        TimeSpan timeLeft = entitylabel.TimeLeft;
                        if (Settings.BorderSettings.ShowTimer && timeLeft.TotalMilliseconds > 0)
                            Graphics.DrawText(timeLeft.ToString(@"mm\:ss"), Settings.BorderSettings.TimerTextSize, rect.TopRight.Translate(4, 0));
                    }
                    Graphics.DrawFrame(rect, Settings.BorderSettings.BorderWidth, borderColor);
                }
            }
            else
            {
                currentLabels = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabels.ToDictionary(y => y.ItemOnGround.Address, y => y);
            }
        }

	
		private Vector2 DrawItem(AlertDrawStyle drawStyle, Vector2 delta, float x, float y, Vector2 vPadding, string text)
		{
			// collapse padding when there's a frame
			vPadding.X -= drawStyle.FrameWidth;
			vPadding.Y -= drawStyle.FrameWidth;
			// item will appear to have equal size

			double phi;
			var distance = delta.GetPolarCoordinates(out phi);


			//text = text + " @ " + (int)distance + " : " + (int)(phi / Math.PI * 180)  + " : " + xSprite;

            float compassOffset = Settings.TextSize + 8;
			var textPos = new Vector2(x - vPadding.X - compassOffset, y + vPadding.Y);
            var vTextSize = Graphics.DrawText(text, Settings.TextSize, textPos, drawStyle.color, FontDrawFlags.Right);

			int iconSize =  drawStyle.IconIndex >= 0 ? vTextSize.Height : 0;

			float fullHeight = vTextSize.Height + 2 * vPadding.Y + 2 * drawStyle.FrameWidth;
			float fullWidth = vTextSize.Width + 2 * vPadding.X + iconSize + 2 * drawStyle.FrameWidth + compassOffset;
            Graphics.DrawBox(new RectangleF(x - fullWidth, y, fullWidth - compassOffset, fullHeight), new ColorBGRA(0, 0, 0, 180));

			var rectUV = GetDirectionsUV(phi, distance);
            Graphics.DrawImage("directions.png", new RectangleF(x - vPadding.X - compassOffset + 6, y + vPadding.Y, vTextSize.Height, vTextSize.Height), rectUV);

			if (iconSize > 0)
			{
                const float ICONS_IN_SPRITE = 4;

                var iconPos = new RectangleF(textPos.X - iconSize - vTextSize.Width, textPos.Y, iconSize, iconSize);
                var iconX = drawStyle.IconIndex / ICONS_IN_SPRITE;
                var uv = new RectangleF(iconX, 0, (drawStyle.IconIndex + 1) / ICONS_IN_SPRITE - iconX, 1);
                Graphics.DrawImage("item_icons.png", iconPos, uv);
			}
			if (drawStyle.FrameWidth > 0)
			{
				var frame = new RectangleF(x - fullWidth, y, fullWidth - compassOffset , fullHeight);
                Graphics.DrawFrame(frame, drawStyle.FrameWidth, drawStyle.color);
			}
			return new Vector2(fullWidth, fullHeight);
		}

		private string GetItemName(KeyValuePair<EntityWrapper, AlertDrawStyle> kv)
		{
			string text;
			EntityLabel labelFromEntity = GameController.EntityListWrapper.GetLabelForEntity(kv.Key);

			if (labelFromEntity == null)
			{
				Entity itemEntity = kv.Key.GetComponent<WorldItem>().ItemEntity;
				if (!itemEntity.IsValid)
					return null;
				text = kv.Value.Text;
			}
			else
			{
				text = labelFromEntity.Text;
			}
			return text;
		}

		private Dictionary<string, CraftingBase> LoadCraftingBases()
		{
			if (!File.Exists("config/crafting_bases.txt"))
			{
				return new Dictionary<string, CraftingBase>();
			}
			Dictionary<string, CraftingBase> dictionary = new Dictionary<string, CraftingBase>(StringComparer.OrdinalIgnoreCase);
			List<string> parseErrors = new List<string>();
			string[] array = File.ReadAllLines("config/crafting_bases.txt");
			foreach (
				string text2 in
					array.Select(text => text.Trim()).Where(text2 => !string.IsNullOrWhiteSpace(text2) && !text2.StartsWith("#")))
			{
				string[] parts = text2.Split(new[]{','});
				string itemName = parts[0].Trim();

				CraftingBase item = new CraftingBase() {Name = itemName};

				int tmpVal = 0;
				if (parts.Length > 1 && int.TryParse(parts[1], out tmpVal))
					item.MinItemLevel = tmpVal;

				if (parts.Length > 2 && int.TryParse(parts[2], out tmpVal))
					item.MinQuality = tmpVal;

				const int RarityPosition = 3;
				if (parts.Length > RarityPosition)
				{
					item.Rarities = new ItemRarity[parts.Length - 3];
					for (int i = RarityPosition; i < parts.Length; i++)
					{
						if (!Enum.TryParse(parts[i], true, out item.Rarities[i - RarityPosition]))
						{
							parseErrors.Add("Incorrect rarity definition at line: " + text2);
							item.Rarities = null;
						}
					}
				}

				if( !dictionary.ContainsKey(itemName))
					dictionary.Add(itemName, item);
				else
					parseErrors.Add("Duplicate definition for item was ignored: " + text2);
			}

			if(parseErrors.Any())
				throw new Exception("Error parsing config/crafting_bases.txt \r\n" + string.Join(Environment.NewLine, parseErrors) + Environment.NewLine + Environment.NewLine);

			return dictionary;
		}
		private HashSet<string> LoadCurrency()
		{
			if (!File.Exists("config/currency.txt"))
				return null;
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string[] array = File.ReadAllLines("config/currency.txt");
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i];
				string text2 = text.Trim();
				if (!string.IsNullOrWhiteSpace(text2))
				{
					hashSet.Add(text2.ToLowerInvariant());
				}
			}
			return hashSet;
		}
	}
}
