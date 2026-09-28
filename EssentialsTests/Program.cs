using System.IO;
using System.Text.Json.Nodes;
using System.Collections.ObjectModel;
using System.Windows;
using Onyxstrap;
using Onyxstrap.Models;
using Onyxstrap.Models.Persistable;
using Onyxstrap.Integrations;
int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
void Reject(Action action, string name) { bool rejected=false; try { action(); } catch { rejected=true; } Check(rejected,name); }
Check(GameFavorites.TryParsePlace("123",out var id) && id==123,"positive place ID accepted");
Check(GameFavorites.TryParsePlace(" https://www.roblox.com/games/123/Example?x=1 ",out id) && id==123,"game URL extracts place ID only");
foreach (string input in new[]{"0","-5","9223372036854775808","https://roblox.com.evil.test/games/123","https://evil.test/?roblox.com/games/123","https://roblox.com@evil.test/games/123","https://roblox.com:444/games/123","http://roblox.com/games/123","https://roblox.com/users/123","1 -account abc"})
 Check(!GameFavorites.TryParsePlace(input,out _),"reject invalid favorite: "+input);
Check(GameFavorites.LaunchUri(123)=="roblox://placeId=123","launch URI contains only validated ID");
Check(new LaunchSettings(new[]{GameFavorites.LaunchUri(123)}).RobloxLaunchArgs == "roblox://placeId=123", "favorite URI reaches the existing Roblox launch flow unchanged");
var favorites = new ObservableCollection<FavoriteGame>();
GameFavorites.Add(favorites,"  Example  ","123");
Check(favorites.Single().Name=="Example","favorite name trimmed");
Reject(()=>GameFavorites.Add(favorites,"Duplicate","123"),"duplicate games rejected");
Reject(()=>GameFavorites.Add(favorites," ","456"),"empty labels rejected");
Reject(()=>GameFavorites.Add(favorites,new string('x',61),"456"),"long labels rejected");
var settings = new Settings { FavoriteGames=favorites, SpotifySnapToEdges=false, SpotifyAccentColor="#123456", DeveloperMode=true, BootstrapperIconCustomLocation="secret-path" };
string backup=PreferenceBackup.Export(settings);
Check(!backup.Contains("secret-path") && !backup.Contains("DeveloperMode") && !backup.Contains("CustomIntegrations") && !backup.Contains("ProtectedToken"),"backup excludes paths, developer mode, commands, and tokens");
var current = new Settings { BootstrapperIconCustomLocation="keep-this-path" };
var restored=PreferenceBackup.Import(backup,current);
Check(restored.FavoriteGames.Single().PlaceId==123 && !restored.SpotifySnapToEdges && restored.SpotifyAccentColor=="#123456","portable settings round trip");
Check(restored.BootstrapperIconCustomLocation=="keep-this-path" && !restored.DeveloperMode,"import preserves excluded local preferences");
Check(current.FavoriteGames.Count==0,"import preview leaves current settings untouched");
PreferenceBackup.Apply(restored,current);
Check(current.FavoriteGames.Single().Name=="Example","apply restores validated preferences");
string Mutate(string key,JsonNode? value) { var root=JsonNode.Parse(backup)!;root["Preferences"]![key]=value;return root.ToJsonString(); }
Reject(()=>PreferenceBackup.Import(Mutate("CustomIntegrations",new JsonArray()),current),"import rejects executable integration fields");
Reject(()=>PreferenceBackup.Import(Mutate("Theme",999),current),"invalid enum rejected");
Reject(()=>PreferenceBackup.Import(Mutate("SpotifyAccentColor","invalid"),current),"invalid color rejected");
Reject(()=>PreferenceBackup.Import(Mutate("SpotifyOverlayKey",0),current),"invalid shortcut rejected");
Reject(()=>PreferenceBackup.Import(Mutate("FavoriteGames",null),current),"null favorites rejected");
Reject(()=>PreferenceBackup.Import(backup.Replace("\"Version\": 1","\"Version\": 2"),current),"future format rejected");
Reject(()=>PreferenceBackup.Import(new string('x',1024*1024+1),current),"oversize backup rejected");
var bounds=new Rect(-1920,0,1920,1080);
var offset=OverlayPlacement.Capture(new Point(-382,808),bounds,380,270,true);
Check(offset.Right && offset.Bottom,"player snaps near bottom-right corner on a negative-coordinate monitor");
var point=OverlayPlacement.Resolve(offset,bounds,380,270);
Check(point==new Point(-392,798),"snap retains twelve-pixel edge spacing");
point=OverlayPlacement.Resolve(offset,new Rect(0,0,2560,1440),340,194);
Check(point==new Point(2208,1234),"right/bottom anchors follow resolution and compact-mode changes");
offset=OverlayPlacement.Capture(new Point(-1918,2),bounds,380,270,true);
Check(OverlayPlacement.Resolve(offset,bounds,380,270)==new Point(-1908,12),"top-left corner snaps with margin");
offset=OverlayPlacement.Capture(new Point(-1918,2),bounds,380,270,false);
Check(OverlayPlacement.Resolve(offset,bounds,380,270)==new Point(-1918,2),"disabled snapping preserves free placement");
point=OverlayPlacement.Resolve(new OverlayOffset{X=double.NaN,Y=double.PositiveInfinity},new Rect(0,0,200,100),380,270);
Check(point==new Point(0,0),"bad offsets and undersized windows remain bounded");
var monitors=new Dictionary<string,OverlayOffset>{{"A",new(){X=35,Y=40}},{"B",new(){X=200,Y=150}}};
var saved=System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,OverlayOffset>>(System.Text.Json.JsonSerializer.Serialize(monitors))!;
Check(saved["A"].X==35 && saved["B"].X==200,"independent monitor offsets survive serialization");
string dir=Path.Combine(AppContext.BaseDirectory,"fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
try { string path=Path.Combine(dir,"backup.json");PreferenceBackup.Write(path,backup);PreferenceBackup.Write(path,backup);Check(File.ReadAllText(path)==backup && Directory.GetFiles(dir).Length==1,"backup atomically replaces its file and leaves no staging file"); }
finally { Directory.Delete(dir,true); }
Console.WriteLine($"{passed} essentials checks passed.");
