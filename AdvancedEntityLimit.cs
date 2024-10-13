using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AdvancedEntityLimit", "M&B-Studios", "1.0.5")]
    class AdvancedEntityLimit : RustPlugin
    {
        #region Classes

        public class EntityData
        {
            public string Image;
            public int Limit;
            public bool IsLimited;
        }
        public class LimitData
        {
            public readonly int Order;
            public readonly Dictionary<string, EntityData> Entities = new();

            public LimitData(LimitData copy)
            {
                Order = copy.Order + 1;
                Entities = copy.Entities;
            }
            public LimitData(int limit = 10)
            {
                Order = 0;
                foreach (var dict in Instance.GetEntities().Where(dict => !Entities.ContainsKey(dict.Value)))
                {
                    Entities.Add(dict.Value, new EntityData
                    {
                        Image = dict.Key,
                        Limit = limit,
                        IsLimited = true
                    });
                }
            }

            [JsonConstructor]
            public LimitData()
            {
            }

            public int GetLimitForEntity(string name)
            {
                if (!Entities.TryGetValue(name, out var data))
                    return -1;
                if (!data.IsLimited)
                    return -1;
                
                return data.Limit;
            }
        }

        private void CreatePlayerLimitUI(BasePlayer player, int placedCount, int limit)
        {
            var container = new CuiElementContainer();

            var panel = new CuiPanel
            {
                RectTransform = { AnchorMin = "0.01 0.01", AnchorMax = "0.1 0.05" }, // Adjust the position and size as needed
                Image = { Color = "0.1 0.1 0.1 0.7" }
            };

            var text = new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = $"{placedCount}/{limit}", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            };

            container.Add(panel, "Overlay", "PlayerLimitPanel");
            container.Add(text, "PlayerLimitPanel");

            CuiHelper.DestroyUi(player, "PlayerLimitPanel");
            CuiHelper.AddUi(player, container);

            // Destroy the UI after 3 seconds
            timer.Once(3f, () => CuiHelper.DestroyUi(player, "PlayerLimitPanel"));
        }

        private void UpdatePlayerLimitUI(BasePlayer player, string prefabName)
        {
            var privilege = GetHighestPrivilege(player);
            if (privilege == null) return;

            int limit = privilege.GetLimitForEntity(prefabName);
            if (limit == -1) limit = 0;

            int placedCount = DeployedByPlayer(player, prefabName);

            CreatePlayerLimitUI(player, placedCount, limit);
        }

        #endregion

        #region Fields

        private static AdvancedEntityLimit Instance;
        private Dictionary<string, LimitData> Limits;

        private enum ActionType
        {
            OpenUI = 0,
            SetLimit = 1,
            CreatePermission = 2
        }

        private const string PERM_UI = "advancedentitylimit.ui";
        private const string PERM_SETLIMIT = "advancedentitylimit.setlimit";
        private const string PERM_CREATEPERM = "advancedentitylimit.createpermission";
        private const string PERM_ADMIN = "advancedentitylimit.admin";

        private const string Layer = "ui.AdvancedEntityLimit.bg";
        private const int ITEMS_PER_PAGE = 27;
        private const int PRIVILEGES_PER_PAGE = 7;
        #endregion

        #region Hooks

        private void OnPlayerInit(BasePlayer player)
        {
            UpdatePlayerLimitUI(player, "entity.prefab"); // Replace "entity.prefab" with actual entity name
        }

        private void OnServerInitialized()
        {
            Instance = this;
            
            LoadData();
            
            if (cfg.AutoFillEntities)
                UpdateLists();
            
            foreach (var x in Limits.Where(x => !permission.PermissionExists(x.Key)))
            {
                permission.RegisterPermission(x.Key, this);
            }
            
            permission.RegisterPermission(PERM_UI, this);
            permission.RegisterPermission(PERM_SETLIMIT, this);
            permission.RegisterPermission(PERM_CREATEPERM, this);
            permission.RegisterPermission(PERM_ADMIN, this);
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(basePlayer, Layer);
                CuiHelper.DestroyUi(basePlayer, Layer + ".close");
            }
            Instance = null;
        }

        private object CanDeployItem(BasePlayer player, Deployer deployer, uint entityId)
        {
            if (deployer == null)
                return null;

            var res = CanPlayerBuild(player, deployer.PrefabName);
            if (res.Value == false)
            {
                player.ChatMessage(cfg.Prefix + cfg.MaxObjectsMessage.Replace("{0}", res.Key.ToString()));
            }
            else
            {
                // Aktualisieren Sie die UI nach dem Platzieren
                timer.Once(0.1f, () => UpdatePlayerLimitUI(player, deployer.PrefabName));
            }
            
            return res.Value ? (bool?)null : false;
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null)
                return null;

            var player = planner.GetOwnerPlayer();
            if (player == null)
                return null;

            var res = CanPlayerBuild(player, prefab.fullName);
            if (res.Value == false)
            {
                player.ChatMessage(cfg.Prefix + cfg.MaxObjectsMessage.Replace("{0}", res.Key.ToString()));
            }
            else
            {
                // Aktualisieren Sie die UI nach dem Platzieren
                timer.Once(0.1f, () => UpdatePlayerLimitUI(player, prefab.fullName));
            }

            return res.Value ? (bool?)null : false;
        }

        #endregion

        #region Methods

        private void UpdateLists()
        {
            bool hasChanges = false;
            var entities = GetEntities();

            foreach (var x in Limits.ToArray())
            {
                foreach (var y in entities.Where(y => !x.Value.Entities.ContainsKey(y.Value)))
                {
                    hasChanges = true;
                    Limits[x.Key].Entities.Add(y.Value, new EntityData
                    {
                        Image = y.Key,
                        Limit = 10,
                        IsLimited = true
                    });
                }
            }

            if (hasChanges)
            {
                PrintWarning("Data was updated");
                SaveData();
            }
        }

        private LimitData GetHighestPrivilege(BasePlayer player)
        {
            LimitData privilege = null;
            foreach (var x in Limits.OrderBy(x => x.Value.Order))
            {
                if (player.IPlayer.HasPermission(x.Key))
                    privilege = x.Value;
            }

            return privilege;
        }

        private string PrefabnameToSprite(string prefabname)
        {
            var res = prefabname.Replace(".prefab", ".png");
            return res;
        }

        private int DeployedByPlayers(IReadOnlyCollection<ulong> player, string prefabname)
        {
            Stopwatch sw = Stopwatch.StartNew();
            
            int count = 0;
            foreach (var x in BaseEntity.serverEntities)
            {
                var y = x as BaseEntity;
                if (player.Any(p => p == y!.OwnerID) && x.PrefabName == prefabname)
                    count++;
            }
            sw.Stop();
            
            if (sw.ElapsedMilliseconds > 100)
                PrintWarning($"[{nameof(DeployedByPlayer)}] => Time - {sw.ElapsedMilliseconds}ms");
            return count;
        }

        private int DeployedByPlayer(BasePlayer player, string prefabname) =>
            DeployedByPlayers(new List<ulong>() { player.userID }, prefabname);

        private KeyValuePair<int, bool> CanPlayerBuild(BasePlayer player, string prefabname)
        {
            if (string.IsNullOrEmpty(prefabname))
                return new KeyValuePair<int, bool>(0, true);
            
            
            var privilege = GetHighestPrivilege(player);
            
            if (privilege == null)
            {
                for (int i = 0; i < 3; i++)
                    PrintError($"Player {player.displayName}({player.UserIDString}) doesnt have any permission! He cant build.");
                
                return new KeyValuePair<int, bool>(-1, false);
            }
            
            var limit = privilege.GetLimitForEntity(prefabname);
            if (limit == -1)
                return new KeyValuePair<int, bool>(0, true);
            List<ulong> players = new List<ulong>() { player.userID };

            var teammates = GetPlayerTeammates(player);
            
            if (teammates != null)
                players.AddRange(teammates);
            
            
            return new KeyValuePair<int, bool>(limit, limit > DeployedByPlayers(players, prefabname));
        }

        private List<ulong> GetPlayerTeammates(BasePlayer player)
        {
            var result = new List<ulong>();
            
            if (cfg.UseTeams && player.Team != null)
            {
                result.AddRange(player.Team.members);
            }

            if (cfg.UseClans && ClanPluginAvailable() && PlayerInClan(player))
            {
                var members = GetClanMembers(player);
                if (members != null)
                    result.AddRange(members.Select(ulong.Parse));
            }

            return result;
        }

        private Plugin _clanPlugin = null;
        private bool CheckedClansPlugin = false;
        private Plugin GetClanPlugin()
        {
            if (_clanPlugin == null && !CheckedClansPlugin)
            {
                _clanPlugin = plugins.GetAll()?.FirstOrDefault(x => x.Author == cfg.ClanAuthor.ToString())!;
                CheckedClansPlugin = true;
            }
            
            return _clanPlugin;
        }

        private bool ClanPluginAvailable() => GetClanPlugin() != null;

        
        private bool PlayerInClan(BasePlayer player)
        {
            var clan = GetClanPlugin();
            if (!clan)
                return false;
            
            return cfg.ClanAuthor switch
            {
                ClansAuthor.Mevent => (string)GetClanPlugin()?.Call("GetClanOf", player.userID) != null,
                ClansAuthor.k1lly0u => (string)GetClanPlugin()?.Call("GetClanOf", player.userID) != null,
                _ => false
            };
        }

        private IReadOnlyCollection<string> GetClanMembers(BasePlayer player)
        {
            var clan = GetClanPlugin();
            if (!clan)
                return null;

            return (List<string>)GetClanPlugin().Call("GetClanMembers", player.userID);
        }

        private bool HasPermission(BasePlayer player, string perm) => player.IPlayer.HasPermission(perm);

        private bool CanUseAction(BasePlayer player, ActionType type)
        {
            if (player == null)
                return true;
            
            if (HasPermission(player, PERM_ADMIN))
                return true;

            return type switch
            {
                ActionType.OpenUI => HasPermission(player, PERM_UI),
                ActionType.SetLimit => HasPermission(player, PERM_SETLIMIT),
                ActionType.CreatePermission => HasPermission(player, PERM_CREATEPERM),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        private Dictionary<string, string> GetEntities()
        {
            var dict = new Dictionary<string, string>();
            foreach (var item in ItemManager.itemList)
            {
                var modDeployable = item.itemMods.FirstOrDefault(x => x.GetComponent<ItemModDeployable>() != null);
                if (modDeployable == null)
                    continue;
                
                if (dict.ContainsKey(item.itemid.ToString()))
                    continue;
                dict.Add(item.itemid.ToString(), modDeployable.GetComponent<ItemModDeployable>().entityPrefab.resourcePath);
            }

            foreach (var x in GameManager.server.preProcessed.prefabList.Where(x =>
                         x.Value.GetComponent<BuildingBlock>() != null))
            {
                if (x.Value.TryGetComponent<BaseEntity>(out var component))
                {
                    if (component.PrefabName.Contains("stairs.spiral") || component.PrefabName.Contains("stairs.u") || component.PrefabName.Contains("stairs.l"))
                        continue;
                    dict.Add(PrefabnameToSprite(component.PrefabName), component.PrefabName);
                }
            }

            return dict;
        }

        private void SendMessage(BasePlayer player, string message, params object[] args)
        {
            if (player != null)
                player.ChatMessage(cfg.Prefix + string.Format(message, args));
            else
                PrintWarning(string.Format(message, args));
        }

        private string ConcatArgs(IReadOnlyList<string> args, int start = 0)
        {
            var sb = new StringBuilder();

            var first = true;
            for (var i = start; i < args.Count; i++)
            {
                if (first)
                {
                    sb.Append(args[i]);
                    first = false;
                    continue;
                }

                sb.Append(" " + args[i]);
            }

            return sb.ToString();
        }

        private Dictionary<string, string> CorrectSprites = new Dictionary<string, string>()
        {
            ["assets/prefabs/building core/wall.low/wall.low.png"] = "assets/prefabs/building core/wall.low/wall.third.png"
        };

        private string ToCorrectSprite(string sprite)
        {
            if (CorrectSprites.TryGetValue(sprite, out var trueSprite))
                return trueSprite;
            return sprite;
        }

        #endregion

        #region UI

        private void UI_DrawPrivilegePages(BasePlayer player, int page = 0)
        {
            var container = new CuiElementContainer();
            
            var list = Limits.Keys;
            
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "-0.055 -0.014", OffsetMax = "-0.055 -0.014" }
            }, Layer, Layer + ".priv.pages");

            container.Add(new CuiButton
            {
                Button = { Color = "0.525 0.502 0.467 0.761", Command = page - 1 >= 0 ? $"ael.changeprivpage {page - 1}" : "" },
                Text = { Text = "<", Font = "robotocondensed-bold.ttf", FontSize = 21, Align = TextAnchor.MiddleCenter, Color = "0.808 0.78 0.741 0.659" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "5.929 -36.907", OffsetMax = "36.94 -5.896" }
            }, Layer + ".priv.pages", Layer + ".priv.pages" + ".prev");

            container.Add(new CuiButton
            {
                Button = { Color = "0.525 0.502 0.467 0.761", Command = list.Skip((page + 1) * PRIVILEGES_PER_PAGE).Any() ? $"ael.changeprivpage {page + 1}" : ""},
                Text = { Text = ">", Font = "robotocondensed-bold.ttf", FontSize = 21, Align = TextAnchor.MiddleCenter, Color = "0.808 0.78 0.741 0.659" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "646.894 -36.907", OffsetMax = "677.906 -5.896" }
            }, Layer + ".priv.pages", Layer + ".priv.pages" + ".next");
            
            CuiHelper.DestroyUi(player, Layer + ".priv.pages");
            CuiHelper.AddUi(player, container);
        }

        private void UI_DrawMain(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement()
            {
                Name = Layer + ".close",
                Parent = "Overlay",
                Components =
                {
                    new CuiButtonComponent()
                    {
                        Command = "ael.close",
                        Color = "0 0 0 0.5",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    }
                }
            });
            
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.317 0.317 0.317 0.749" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-377.06 -221.965", OffsetMax = "377.06 221.965" }
            }, "Overlay", Layer);

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, Layer + ".close");
            CuiHelper.AddUi(player, container);

            var privilege = Limits.FirstOrDefault();

            if (privilege.Equals(default(KeyValuePair<string, LimitData>)))
            {
                PrintError("Limits list is empty!");
                PrintError("Limits list is empty!");
                PrintError("Limits list is empty!");
                return;
            }
            UI_DrawPrivileges(player, privilege.Key);
            UI_DrawPrivilegePages(player);
            UI_DrawEntities(player, privilege.Key);
            UI_DrawPages(player, privilege.Key);
        }

        private void UI_DrawEntities(BasePlayer player, string privilege, string search = "", int page = 0)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiButton
            {
                Button = { Color = "0.647 0.624 0.6 1", Command = $"ael.opensearch {privilege}" },
                Text = { Text = "SEARCH", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.317 0.317 0.317 1" },
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-70.309 -36.809", OffsetMax = "-4.9 -5.991" }
            }, Layer, Layer + ".search");
            CuiHelper.DestroyUi(player, Layer + ".search");
            CuiHelper.AddUi(player, container);
            
            container.Clear();
            
            CuiHelper.DestroyUi(player, Layer + ".entities");
            
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-358.19 -184.915",
                    OffsetMax = "385.16 174.4"
                }
            }, Layer, Layer + ".entities");


            IEnumerable<KeyValuePair<string, EntityData>> list = Limits[privilege].Entities;
            if (!string.IsNullOrEmpty(search))
            {
                list = list.Where(x => x.Key.Contains(search, CompareOptions.IgnoreCase));
            }
            
            float minx = -371.6701f;
            float maxx = -294.628f;
            float miny = 102.618f;
            float maxy = 179.6601f;
            int i = 0;
            CuiHelper.AddUi(player, container);
            foreach (var entity in list.Skip(page * ITEMS_PER_PAGE).Take(ITEMS_PER_PAGE))
            {
                container.Clear();
                if (i % 9 == 0 && i != 0)
                {
                    minx = -371.6701f;
                    maxx = -294.628f;
                    miny -= 108.139f;
                    maxy -= 108.139f;
                }

                var image = new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0 0 0 0" },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{minx} {miny}",
                        OffsetMax = $"{maxx} {maxy}"
                    }
                };

                if (int.TryParse(entity.Value.Image, out var itemid) == false)
                    image.Image = new CuiImageComponent()
                    {
                        Color = "1 1 1 0.8",
                        Sprite = ToCorrectSprite(entity.Value.Image),
                        FadeIn = 0.1f
                    };
                else
                    image.Image = new CuiImageComponent()
                    {
                        ItemId = itemid,
                        FadeIn = 0.1f
                    };
                container.Add(image, Layer + ".entities", Layer + ".entities" + $".entity.{entity.Key}");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color =  "0.808 0.78 0.741 0.4",
                        FadeIn = 0.1f },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "21.421 21.122",
                        OffsetMax = "35.778 35.479"
                    }
                }, Layer + ".entities" + $".entity.{entity.Key}", Layer + ".entities" + $".entity.{entity.Key}" + ".state");

                container.Add(new CuiButton
                    {
                        Button = { Color = entity.Value.IsLimited ? "1 1 1 0.6" : "0 0 0 0",
                            FadeIn = 0.1f, Command = $"ael.setenabled {privilege} {!entity.Value.IsLimited} {entity.Key}", Sprite = "assets/icons/check.png"},
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-7.029 -7.179",
                            OffsetMax = "7.178 7.178"
                        }
                    }, Layer + ".entities" + $".entity.{entity.Key}" + ".state",
                    Layer + ".entities" + $".entity.{entity.Key}" + ".state" + ".check");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.525 0.502 0.467 0.761",
                        FadeIn = 0.1f },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-38.521 -66.937",
                        OffsetMax = "38.521 -39.863"
                    }
                }, Layer + ".entities" + $".entity.{entity.Key}", Layer + ".entities" + $".entity.{entity.Key}" + ".settings");

                container.Add(new CuiElement
                {
                    Name = Layer + ".entities" + $".entity.{entity.Key}" + ".settings" + ".limit.label",
                    Parent = Layer + ".entities" + $".entity.{entity.Key}" + ".settings",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "Limit", Font = "robotocondensed-bold.ttf", FontSize = 10,
                            Align = TextAnchor.UpperCenter, Color = "0.808 0.78 0.741 1",
                            FadeIn = 0.1f
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-38.521 0",
                            OffsetMax = "38.521 13.537"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = Layer + ".entities" + $".entity.{entity.Key}" + ".settings" + ".limit.input",
                    Parent = Layer + ".entities" + $".entity.{entity.Key}" + ".settings",
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Color = "1 1 1 1", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter,
                            CharsLimit = 9, IsPassword = false, Text = entity.Value.Limit.ToString(), NeedsKeyboard = true,
                            Command = $"ael.setlimit {entity.Key}| {privilege} "
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-38.521 -13.537",
                            OffsetMax = "38.521 0"
                        }
                    }
                });
                
                minx += 79.549f;
                maxx += 79.549f;
                i++;
                CuiHelper.AddUi(player, container);
            }
        }

        private void UI_DrawPages(BasePlayer player, string privilege, string search = "", int page = 0)
        {
            var container = new CuiElementContainer();
            
            IEnumerable<KeyValuePair<string, EntityData>> list = Limits[privilege].Entities;
            if (!string.IsNullOrEmpty(search))
            {
                list = list.Where(x => x.Key.Contains(search, CompareOptions.IgnoreCase));
            }

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "272.16 -219.162",
                    OffsetMax = "372.16 -188.438"
                }
            }, Layer, Layer + ".pages");

            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "1 1 1 0", Command = page - 1 >= 0 ? $"ael.changepage {privilege} {page - 1} {search}" : ""
                },
                Text =
                {
                    Text = "<", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter,
                    Color = "0.808 0.78 0.741 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -15.362", OffsetMax = "-18.37 15.362"
                }
            }, Layer + ".pages", Layer + ".pages" + ".previous");

            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "1 1 1 0",
                    Command = list.Skip((page + 1) * ITEMS_PER_PAGE).Any()
                        ? $"ael.changepage {privilege} {page + 1} {search}"
                        : ""
                },
                Text =
                {
                    Text = ">", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter,
                    Color = "0.808 0.78 0.741 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "18.37 -15.362", OffsetMax = "50 15.362"
                }
            }, Layer + ".pages", Layer + ".pages" + ".next");
            
            CuiHelper.DestroyUi(player, Layer + ".pages");
            CuiHelper.AddUi(player, container);
        }

        private void UI_DrawSearch(BasePlayer player, string privilege)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.525 0.502 0.467 0.675" },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "188.998 153.695",
                    OffsetMax = "372.16 181.105"
                }
            }, Layer, Layer + ".search.bg");

            container.Add(new CuiElement
            {
                Name = Layer + ".search.bg" + ".input",
                Parent = Layer + ".search.bg",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Color = "1 1 1 0.7", Font = "robotocondensed-bold.ttf", FontSize = 17,
                        Align = TextAnchor.MiddleRight, CharsLimit = 60, NeedsKeyboard = true, Command = $"ael.confirmsearch {privilege} "
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-88.2 -13.705",
                        OffsetMax = "88.457 13.705"
                    }
                }
            });
            
            CuiHelper.DestroyUi(player, Layer + ".search.bg");
            CuiHelper.AddUi(player, container);
        }

        private void UI_DrawPrivileges(BasePlayer player, string privilege = "", int page = 0)
        {
            var container = new CuiElementContainer();
            
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "190.87 -36.84", OffsetMax = "618.097 -6.019" }
            }, Layer, Layer + ".privileges");

            float minx = -336.11f;
            float maxx = -258.8409f;  
            float miny = -15.41024f;
            float maxy = 15.40976f;
            var list = Limits.Keys.Skip(page * PRIVILEGES_PER_PAGE).Take(PRIVILEGES_PER_PAGE);
            for (var i = 0; i < list.Count(); i++) 
            {
                var key = list.ElementAtOrDefault(i);
                
                container.Add(new CuiButton
                {
                    Button =
                    {
                        Color = "0.525 0.502 0.467 1",
                        Command = privilege == key ? "" : $"ael.selectprivilege {key} {page}"
                    },
                    Text =
                    {
                        Text = key.Replace("advancedentitylimit.", "", StringComparison.OrdinalIgnoreCase),
                        Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter,
                        Color = "0.808 0.78 0.741 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{minx} {miny}",
                        OffsetMax = $"{maxx} {maxy}"
                    }
                }, Layer + ".privileges", Layer + ".privileges" + $".privilege.{i}");
                
                minx += 78.275f;
                maxx += 78.275f;
            }
            
            CuiHelper.DestroyUi(player, Layer + ".privileges");
            CuiHelper.AddUi(player, container);
        }

        private void UI_UpdateEntity(BasePlayer player, string privilege, string prefabname)
        {
            var entity = Limits[privilege].Entities[prefabname];
            
            var container = new CuiElementContainer();
            
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color =  "0.808 0.78 0.741 0.4",
                    FadeIn = 0.1f },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "21.421 21.122",
                    OffsetMax = "35.778 35.479"
                }
            }, Layer + ".entities" + $".entity.{prefabname}", Layer + ".entities" + $".entity.{prefabname}" + ".state");

            container.Add(new CuiButton
                {
                    Button = { Color = entity.IsLimited ? "1 1 1 0.6" : "0 0 0 0",
                        FadeIn = 0.1f, Command = $"ael.setenabled {privilege} {!entity.IsLimited} {prefabname}", Sprite = "assets/icons/check.png"},
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-7.029 -7.179",
                        OffsetMax = "7.178 7.178"
                    }
                }, Layer + ".entities" + $".entity.{prefabname}" + ".state",
                Layer + ".entities" + $".entity.{prefabname}" + ".state" + ".check");
            
            CuiHelper.DestroyUi(player, Layer + ".entities" + $".entity.{prefabname}" + ".state");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Commands
        [ConsoleCommand("ael.close")]
        private void cmdClose(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            
            CuiHelper.DestroyUi(arg.Player(), Layer);
            CuiHelper.DestroyUi(arg.Player(), Layer + ".close");
        }

        [ConsoleCommand("ael.changeprivpage")]
        private void cmdChangePrivPage(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || arg.Args.IsNullOrEmpty())
                return;
            
            if (!int.TryParse(arg.Args[0], out var page))
                return;
            
            UI_DrawPrivileges(arg.Player(), "", page);
            UI_DrawPrivilegePages(arg.Player(), page);
        }
        [ConsoleCommand("ael.changepage")]
        private void cmdChangePage(ConsoleSystem.Arg arg)
        {
            
            if (arg.Player() == null || arg.Args.IsNullOrEmpty())
                return;

            if (arg.Args.Length < 2)
                return;

            string privilege = arg.Args[0];
            if (!int.TryParse(arg.Args[1], out var page))
                return;

            string pattern = ConcatArgs(arg.Args, 2);
            
            UI_DrawEntities(arg.Player(), privilege, pattern, page);
            UI_DrawPages(arg.Player(), privilege, pattern, page);
        }
        [ChatCommand("limits")]
        private void cmdLimits(BasePlayer player)
        {
            if (!CanUseAction(player, ActionType.OpenUI))
            {
                return;
            }
            
            UI_DrawMain(player);
        }
        
        [ConsoleCommand("ael.selectprivilege")]
        private void cmdSelectPrivilege(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || arg.Args.IsNullOrEmpty())
                return;

            string privilege = arg.Args[0];
            
            if (!int.TryParse(arg.Args[1], out var page))
                return;
            
            UI_DrawEntities(arg.Player(), privilege);
            UI_DrawPrivileges(arg.Player(), privilege, page);
            UI_DrawPages(arg.Player(), privilege);
        }
        [ConsoleCommand("ael.confirmsearch")]
        private void cmdConfirmSearch(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;

            if (arg.Args.IsNullOrEmpty() || arg.Args.Length < 2)
                return;
            
            string privilege = arg.Args[0];
            string pattern = ConcatArgs(arg.Args, 1);


            CuiHelper.DestroyUi(arg.Player(), Layer + ".search.bg");
            
            UI_DrawEntities(arg.Player(), privilege, pattern);
            UI_DrawPages(arg.Player(), privilege, pattern);
        }
        [ConsoleCommand("ael.opensearch")]
        private void cmdOpenSearch(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            
            UI_DrawSearch(arg.Player(), arg.Args[0]);
        }
        [ConsoleCommand("ael.setenabled")]
        private void cmdSetEnabled(ConsoleSystem.Arg arg)
        {
            if (!CanUseAction(arg.Player(), ActionType.SetLimit))
            {
                SendMessage(arg.Player(), "You cant use this command");
                return;
            }

            if (arg.Args.IsNullOrEmpty() || arg.Args.Length < 3)
                return;
            
            string privilege = arg.Args[0];
            string prefabname = ConcatArgs(arg.Args, 2);
            
            if (!bool.TryParse(arg.Args[1], out var state))
                return;
            
            if (!Limits.ContainsKey(privilege))
                return;
            if (!Limits[privilege].Entities.ContainsKey(prefabname))
                return;

            Limits[privilege].Entities[prefabname].IsLimited = state;
            SaveData();
            if (arg.Player() != null)
                UI_UpdateEntity(arg.Player(), privilege, prefabname);
        }
        [ConsoleCommand("ael.setlimit")]
        private void cmdSetLimit(ConsoleSystem.Arg arg)
        {
            if (!CanUseAction(arg.Player(), ActionType.SetLimit))
            {
                SendMessage(arg.Player(), "You cant use this command");
                return;
            }
            
            if (arg.Args.IsNullOrEmpty() || arg.Args.Length < 3)
            {
                return;
            }

            var fullstring = ConcatArgs(arg.Args, 0);
            var index = fullstring.IndexOf("|");
            var prefabname = ConcatArgs(arg.Args, 0)[..index].Trim();
            
            
            string privilege = fullstring.Replace(prefabname + "|", "").Split(" ")[1];
            
            if (!int.TryParse(fullstring.Replace(prefabname + "|", "").Split(" ")[2], out var newLimit))
                return;
         
            if (!Limits.ContainsKey(privilege))
                return;
            
            if (!Limits[privilege].Entities.ContainsKey(prefabname))
                return;

            Limits[privilege].Entities[prefabname].Limit = newLimit;
            SaveData();
        }

        [ConsoleCommand("ael.createnew")]
        private void cmdCreateNew(ConsoleSystem.Arg arg)
        {
            if (!CanUseAction(arg.Player(), ActionType.CreatePermission))
            {
                SendMessage(arg.Player(), "You cant use this command");
                return;
            }

            if (arg.Args.IsNullOrEmpty())
            {
                SendMessage(arg.Player(), "Usage: ael.createnew <advancedentitylimit.NAME> <optional: copy from advancedentitylimit.NAME>");
                return;
            }
            
            string name = arg.Args[0];
            if (!name.StartsWith("advancedentitylimit"))
            {
                SendMessage(arg.Player(), "Usage: ael.createnew <advancedentitylimit.NAME> <optional: copy from advancedentitylimit.NAME>");
                return;
            }

            if (Limits.ContainsKey(name))
            {
                SendMessage(arg.Player(), $"Privilege '{name}' already exists!");
                return;
            }

            LimitData data = new(10);
            string copyfrom = arg.Args.ElementAtOrDefault(1)!;
            if (!string.IsNullOrEmpty(copyfrom))
            {
                if (!Limits.ContainsKey(copyfrom))
                {
                    SendMessage(arg.Player(), $"Failed on copy entities from '{copyfrom}'! '{copyfrom}' not exists.");
                    return;
                }

                data = new LimitData(Limits[copyfrom]);
            }
            
            Limits.Add(name, data);
            SendMessage(arg.Player(), $"Privilege '{name}' successfully created!");
            SaveData();
        }

        #endregion

        #region Config

        private ConfigData cfg;

        internal enum ClansAuthor
        {
            Mevent = 0,
            k1lly0u = 1
        }
        public class ConfigData
        {
            [JsonProperty("Auto-fill missing entities?")]
            public bool AutoFillEntities;
            [JsonProperty("Chat prefix")] 
            public string Prefix;
            [JsonProperty("A message when the player reaches the maximum limit of objects")]
            public string MaxObjectsMessage;

            [JsonProperty("Use teams for sum them constructions")]
            public bool UseTeams;
            [JsonProperty("Use clans?")]
            public bool UseClans;
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty("Clans plugin for sum player constructions (Mevent, k1lly0u)")]
            public ClansAuthor ClanAuthor;
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData()
            {
                AutoFillEntities = true,
                Prefix = "<color=red>[Limits]</color>: ",
                MaxObjectsMessage = "You have <color=red>reached</color> the limit of this object ({0})"
            };
            SaveConfig(config);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            cfg = Config.ReadObject<ConfigData>();
            SaveConfig(cfg);
        }

        private void SaveConfig(object config)
        {
            Config.WriteObject(config, true);
        }

        #endregion

        #region Data

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/limits", Limits);
        }

        private void TryLoadDefaultValues()
        {
            if (Limits.IsNullOrEmpty())
            {
                Limits = new Dictionary<string, LimitData>()
                {
                    ["advancedentitylimit.default"] = new(50),
                    ["advancedentitylimit.vip"] = new(500),
                    ["advancedentitylimit.admin"] = new(int.MaxValue - 1)
                };
                SaveData();
                Interface.Oxide.ReloadPlugin(Title);
            }
        }

        void LoadData()
        {
            Limits = Interface.Oxide?.DataFileSystem?.ReadObject<Dictionary<string, LimitData>>($"{Title}/limits")
                    ?? new Dictionary<string, LimitData>();
            TryLoadDefaultValues();
        }

        #endregion
    }
}
