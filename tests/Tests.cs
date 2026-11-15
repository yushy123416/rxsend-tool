using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RxToolbox {
 internal static class Tests {
  static int count;
  sealed class FakeNvidia : INvidiaProfileApi {
   public string Name="Garry's Mod",Registered="game.exe",DeletedApplication;public bool Mapped=true,HasProfile=true,FailSave;public int Saves;public IntPtr ApplicationProfile=new IntPtr(1);
   public readonly System.Collections.Generic.List<string> Calls=new System.Collections.Generic.List<string>();
   public readonly System.Collections.Generic.Dictionary<uint,NvidiaSettingBackup> Values=new System.Collections.Generic.Dictionary<uint,NvidiaSettingBackup>();
   public IntPtr Application(string path,out string registered){registered=Registered;return Mapped?ApplicationProfile:IntPtr.Zero;}
   public IntPtr Profile(string name){return HasProfile?new IntPtr(1):IntPtr.Zero;}
   public string ProfileName(IntPtr profile){return Name;}
   public IntPtr CreateProfile(string name){Name=name;HasProfile=true;Calls.Add("create");return new IntPtr(1);}
   public void AddApplication(IntPtr profile,string path){Mapped=true;Registered=path;Calls.Add("add");}
   public void DeleteApplication(IntPtr profile,string name){Mapped=false;DeletedApplication=name;Calls.Add("remove");}
   public NvidiaSettingBackup Read(IntPtr profile,uint id){NvidiaSettingBackup s;return Values.TryGetValue(id,out s)?new NvidiaSettingBackup{Id=id,Value=s.Value,UserValue=s.UserValue}:new NvidiaSettingBackup{Id=id};}
   public void Write(IntPtr profile,uint id,uint value){Values[id]=new NvidiaSettingBackup{Id=id,Value=value,UserValue=true};Calls.Add("write");}
   public void DeleteSetting(IntPtr profile,uint id){Values.Remove(id);Calls.Add("delete");}
   public void Save(){Calls.Add("save");if(FailSave)throw new IOException("driver save failed");Saves++;}
  }
  static void PrivacyChecks(){
   using(var rounds=new RoundService(new Settings())){
    Check(rounds.Servers.Count==10&&rounds.Servers.All(s=>s.Key.StartsWith("server-")&&!s.Key.Contains(s.Host)),"encrypted catalog loads and selection identifiers contain no endpoints");
    var server=rounds.Servers[0];var old=new Settings{SelectedServer=server.Host+":"+server.Port,TimerServer=rounds.Servers[1].Host+":"+rounds.Servers[1].Port};
    using(var migrated=new RoundService(old))Check(migrated.Selected.Key==server.Key&&migrated.TimerSelected.Key==rounds.Servers[1].Key,"legacy selections migrate without losing selected servers");
    Check(new ServerEntry{Host=server.Host,Port=server.Port}.ShortName=="未命名服务器","unnamed server never falls back to its endpoint");
    Check(!new ServerEntry{Name="测试 "+server.Host,Host=server.Host}.ShortName.Contains(server.Host),"remote names redact embedded game addresses");
   }
   string message=Privacy.Redact("https://example.com/status.php 192.0.2.1:27015 [2001:db8::1]:27015 ::1");
   Check(!message.Contains("example.com")&&!message.Contains("192.0.2.1")&&!message.Contains("2001:db8")&&!message.Contains("::1"),"URLs and IPv4 and IPv6 addresses are redacted");
   Check(Privacy.Redact("①服 · 回合中 · 12 / 64 · 00:30")=="①服 · 回合中 · 12 / 64 · 00:30","ordinary server labels and countdown remain unchanged");
   var settings=Settings.FromJson("{\"ApiUrl\":\"https://example.com/private\",\"SelectedServer\":\"127.0.0.1:27015\"}");
   string serialized=Json.Write(settings);
   Check(settings.ApiUrl=="https://example.com/private"&&!serialized.Contains("example.com")&&!serialized.Contains("127.0.0.1")&&!serialized.Contains("\"ApiUrl\""),"legacy URL migrates to user-bound encryption and JSON contains no raw endpoints");
   Check(Settings.FromJson(serialized).ApiUrl==settings.ApiUrl,"protected custom URL round trips on the current Windows account");
   settings.ApiUrl=Settings.DefaultApi;Check(settings.ProtectedApiUrl==null&&settings.ApiUrl==Settings.DefaultApi,"default API is resolved from encrypted catalog without persisting URL");
  }
  static void NvidiaChecks(){
   Check(System.Runtime.InteropServices.Marshal.SizeOf(typeof(NvidiaApi.DrsSetting))==12320&&System.Runtime.InteropServices.Marshal.OffsetOf(typeof(NvidiaApi.DrsSetting),"Current").ToInt32()==8220,"NVAPI setting ABI matches NVIDIA header including both 4100-byte unions");
   Check(System.Runtime.InteropServices.Marshal.SizeOf(typeof(NvidiaApi.DrsApplication))==20492&&System.Runtime.InteropServices.Marshal.SizeOf(typeof(NvidiaApi.DrsProfile))==4116,"NVAPI application v4 and profile ABI match NVIDIA headers");
   var api=new FakeNvidia();api.Values[NvidiaFilters.SettingIds[0]]=new NvidiaSettingBackup{Id=NvidiaFilters.SettingIds[0],Value=7,UserValue=true};
   NvidiaFilterBackup snapshot=null;NvidiaFilters.StageApply(api,"game.exe",b=>{snapshot=b;api.Calls.Add("backup");});
   Check(api.Calls[0]=="backup"&&api.Saves==1&&snapshot.Settings[0].Value==7&&snapshot.Settings[0].UserValue,"original user override backed up before any driver writes");
   Check(NvidiaFilters.SettingIds.All(id=>api.Values[id].Value==1),"all three permissions staged and saved together");
   NvidiaFilters.StageRestore(api,snapshot);Check(api.Values.Count==1&&api.Values[NvidiaFilters.SettingIds[0]].Value==7&&api.Saves==2,"restore preserves custom value and removes overrides for inherited defaults");
   api=new FakeNvidia{Name="Half-Life 2"};Reject(()=>NvidiaFilters.StageApply(api,"game.exe",b=>{}),"shared other-game driver profile cannot be modified");Check(api.Calls.Count==0,"rejected shared profile produces no mutations");
   api=new FakeNvidia();Reject(()=>NvidiaFilters.StageApply(api,"game.exe",b=>{throw new IOException("disk full");}),"backup persistence failure aborts configuration");Check(api.Calls.Count==0,"failed backup prevents application changes and driver save");
   api=new FakeNvidia{Mapped=false,HasProfile=false};NvidiaFilters.StageApply(api,"game.exe",b=>snapshot=b);Check(api.Name==NvidiaFilters.OwnProfile&&snapshot.AddedApplication=="game.exe"&&api.Calls.IndexOf("add")<api.Calls.IndexOf("save"),"unmapped executable gets a dedicated GMod profile and recoverable association");
   NvidiaFilters.StageRestore(api,snapshot);Check(!api.Mapped&&api.Values.Count==0,"restore removes only the application association added by toolbox");
   const string originalPath=@"E:\SteamLibrary\steamapps\common\GarrysMod\gmod_win64.exe";
   const string driverPath="e:/steamlibrary/steamapps/common/garrysmod/gmod_win64.exe";
   api=new FakeNvidia{Mapped=false};NvidiaFilters.StageApply(api,originalPath,b=>snapshot=b);api.Registered=driverPath;
   NvidiaFilters.StageRestore(api,snapshot);Check(!api.Mapped&&api.Values.Count==0&&api.DeletedApplication==driverPath,"restore accepts real DRS path normalization and deletes using the exact registered name");
   var normalizedBackup=Json.Read<NvidiaFilterBackup>(Json.Write(snapshot));normalizedBackup.AddedApplication=driverPath;NvidiaFilters.ValidateBackup(normalizedBackup);Check(true,"existing backup remains valid with either Windows or DRS path separators");
   api=new FakeNvidia{Mapped=false};NvidiaFilters.StageApply(api,originalPath,b=>snapshot=b);api.Registered="e:/other/garrysmod/gmod_win64.exe";api.Calls.Clear();Reject(()=>NvidiaFilters.StageRestore(api,snapshot),"same executable name in another folder still blocks restoration");Check(api.Calls.Count==0,"different full path cannot trigger any restore mutations");
   api.Registered=driverPath;api.ApplicationProfile=new IntPtr(2);Reject(()=>NvidiaFilters.StageRestore(api,snapshot),"changed driver profile still blocks restoration despite matching normalized path");Check(api.Calls.Count==0,"changed profile cannot trigger any restore mutations");
   api=new FakeNvidia{FailSave=true};snapshot=null;Reject(()=>NvidiaFilters.StageApply(api,"game.exe",b=>snapshot=b),"driver save failure is surfaced");Check(snapshot!=null&&!snapshot.Applied&&api.Saves==0,"save failure retains the original recovery snapshot");
   api=new FakeNvidia();NvidiaFilters.StageApply(api,"game.exe",b=>snapshot=b);api.Values[NvidiaFilters.SettingIds[2]].Value=9;api.Calls.Clear();Reject(()=>NvidiaFilters.StageRestore(api,snapshot),"restore refuses later edits by another tool");Check(api.Calls.Count==0,"conflict detection completes before restore mutations");
   var invalid=Json.Read<NvidiaFilterBackup>(Json.Write(snapshot));invalid.Settings[0].Id=123;Reject(()=>NvidiaFilters.ValidateBackup(invalid),"backup cannot target arbitrary driver settings");
   invalid=Json.Read<NvidiaFilterBackup>(Json.Write(snapshot));invalid.Settings[0]=null;Reject(()=>NvidiaFilters.ValidateBackup(invalid),"null backup entries are rejected");Reject(()=>NvidiaFilters.ResultPath("../../other"),"helper result token cannot escape the settings directory");
   string fixture=Path.Combine(Path.GetTempPath(),"rx-nvidia-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
   try{string game=Path.Combine(fixture,"gmod_win64.exe");File.WriteAllText(game,"");Reject(()=>NvidiaFilters.ValidateGame(game),"matching executable name alone cannot identify a GMod installation");Directory.CreateDirectory(Path.Combine(fixture,"garrysmod"));File.WriteAllText(Path.Combine(fixture,"garrysmod","gameinfo.txt"),"GameInfo {}");Check(NvidiaFilters.ValidateGame(game)==game,"GMod executable is validated using its installation marker");Directory.CreateDirectory(Path.Combine(fixture,"steamapps"));File.WriteAllText(Path.Combine(fixture,"steamapps","libraryfolders.vdf"),"\"libraryfolders\" { \"0\" { \"path\" \"E:\\\\SteamLibrary\" } \"1\" { \"path\" \"E:\\\\SteamLibrary\" } }");var libraries=NvidiaFilters.LibraryPaths(fixture).ToArray();Check(libraries.Length==2&&libraries[1]==@"E:\SteamLibrary","Steam library discovery decodes VDF paths and removes duplicates");}finally{Directory.Delete(fixture,true);}
  }
  static async Task AutoJoinChecks(){
   var clock=new DateTime(2026,11,12,0,0,0,DateTimeKind.Utc);var server=new ServerEntry{Host="127.0.0.1",Port=27015,Name="测试服"};int queries=0,launches=0;string launched=null;var response=new A2sInfo{Players=70,MaxPlayers=70};
   using(var join=new AutoJoinService((h,p)=>{queries++;return response;},uri=>{launches++;launched=uri;},()=>clock)){
    join.Start(server);await join.PollAsync();Check(join.Running&&launches==0&&queries==1,"auto join waits while server is full");
    await join.PollAsync();Check(queries==1,"auto join enforces one-second query interval");
    clock=clock.AddSeconds(1);response=new A2sInfo{Players=69,MaxPlayers=70};await join.PollAsync();await join.PollAsync();Check(!join.Running&&launches==1&&launched=="steam://connect/127.0.0.1:27015","auto join launches exactly once when a fresh query finds a slot");
    clock=clock.AddSeconds(10);Check(join.Elapsed==TimeSpan.FromSeconds(1),"auto join elapsed time freezes when a connection is launched");
    join.Start(server);clock=clock.AddSeconds(3);join.Stop();clock=clock.AddSeconds(5);Check(join.Elapsed==TimeSpan.FromSeconds(3),"cancelled auto join retains its elapsed time");
    join.Start(server);Check(join.Elapsed==TimeSpan.Zero,"starting another auto join resets its elapsed time");join.Stop();
   }
   queries=0;launches=0;
   using(var join=new AutoJoinService((h,p)=>{queries++;if(queries==1)throw new IOException("timeout");return new A2sInfo{Players=1,MaxPlayers=2};},uri=>launches++,()=>clock)){
    join.Start(server);await join.PollAsync();Check(join.Running&&launches==0,"query failures keep waiting without launching");
    clock=clock.AddSeconds(1);await join.PollAsync();Check(launches==1&&!join.Running,"auto join recovers on a later valid query");
   }
   launches=0;
   using(var join=new AutoJoinService((h,p)=>new A2sInfo{Players=0,MaxPlayers=0},uri=>launches++,()=>clock)){join.Start(server);await join.PollAsync();Check(join.Running&&launches==0,"unknown capacity never triggers auto join");join.Stop();}
   using(var release=new System.Threading.ManualResetEventSlim(false)){
    var entered=new TaskCompletionSource<bool>();launches=0;
    using(var join=new AutoJoinService((h,p)=>{entered.TrySetResult(true);if(!release.Wait(5000))throw new TimeoutException();return new A2sInfo{Players=1,MaxPlayers=2};},uri=>launches++,()=>clock)){
     join.Start(server);var pending=join.PollAsync();await entered.Task;join.Stop();release.Set();await pending;Check(!join.Running&&launches==0,"cancelling an in-flight query prevents later launch");
    }
   }
   using(var release=new System.Threading.ManualResetEventSlim(false)){
    var entered=new TaskCompletionSource<bool>();launches=0;queries=0;launched=null;
    using(var join=new AutoJoinService((h,p)=>{if(++queries==1){entered.TrySetResult(true);if(!release.Wait(5000))throw new TimeoutException();}return new A2sInfo{Players=1,MaxPlayers=2};},uri=>{launches++;launched=uri;},()=>clock)){
     join.Start(server);var pending=join.PollAsync();await entered.Task;join.Start(new ServerEntry{Host="127.0.0.2",Port=27016,Name="新目标"});await join.PollAsync();Check(queries==1,"restarting cannot create overlapping queries");release.Set();await pending;Check(launches==0&&join.Running,"previous target result cannot launch after restart");await join.PollAsync();Check(launches==1&&launched=="steam://connect/127.0.0.2:27016","restarted queue launches only the new target");
    }
   }
   launches=0;
   using(var join=new AutoJoinService((h,p)=>new A2sInfo{Players=1,MaxPlayers=2},uri=>{launches++;throw new IOException("Steam missing");},()=>clock)){join.Start(server);await join.PollAsync();clock=clock.AddSeconds(10);await join.PollAsync();Check(!join.Running&&launches==1&&join.Status.Contains("无法发起进服"),"Steam launch failure stops rather than repeatedly launching");}
   launches=0;
   using(var join=new AutoJoinService((h,p)=>new A2sInfo{Players=1,MaxPlayers=2},uri=>System.Threading.Interlocked.Increment(ref launches),()=>clock)){
    join.Start(new[]{server,new ServerEntry{Host="127.0.0.2",Port=27016,Name="第二目标"}});await join.PollAsync();Check(launches==1&&!join.Running,"simultaneous available targets claim exactly one connection");
   }
   Reject(()=>SteamConnect.BuildUri("127.0.0.1;+quit",27015),"reject command injection in connection target");
   Reject(()=>SteamConnect.BuildUri("127.0.0.1",0),"reject invalid game port");
   Check(SteamConnect.BuildUri("::1",27015)=="steam://connect/[::1]:27015","IPv6 connection target is bracketed");
   var direct=SteamConnect.BuildStartInfo(SteamConnect.BuildUri("127.0.0.1",27015));
   Check(direct.UseShellExecute&&direct.FileName=="steam://rungame/4000/76561202255233023/%2Bconnect%20127.0.0.1%3A27015"&&direct.Arguments=="","Steam uses Neobreach rungame protocol to request GMod connection");
   var fallback=SteamConnect.BuildStartInfo(SteamConnect.BuildUri("::1",27015));
   Check(fallback.UseShellExecute&&fallback.FileName=="steam://rungame/4000/76561202255233023/"+Uri.EscapeDataString("+connect [::1]:27015"),"rungame protocol preserves IPv6 connect arguments");
   Reject(()=>SteamConnect.BuildStartInfo("steam://connect/127.0.0.1:27015 +quit"),"launch rejects injected arguments");
  }
  static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
  static void Reject(Action action,string name){try{action();}catch{Check(true,name);return;}throw new Exception("Expected rejection: "+name);}
  static string Encode(byte[] b,bool legacy){b[0]=(byte)(b.Skip(1).Sum(x=>(int)x)&255);BigInteger n=0;foreach(byte x in b)n=(n<<8)+x;const string a="ABCDEFGHJKLMNOPQRSTUVWXYZabcdefhijkmnopqrstuvwxyz23456789";var s=new StringBuilder();int len=legacy?25:44;for(int i=0;i<len;i++){s.Append(a[(int)(n%57)]);n/=57;}return legacy?"CSGO-"+string.Join("-",Enumerable.Range(0,5).Select(i=>s.ToString().Substring(i*5,5))):"CS"+s;}
  static byte[] InfoPacket(){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(-1);w.Write((byte)0x49);w.Write((byte)17);foreach(string s in new[]{"测试服务器","rx_site","garrysmod","RXBreach"}){w.Write(Encoding.UTF8.GetBytes(s));w.Write((byte)0);}w.Write((ushort)4000);w.Write((byte)12);w.Write((byte)64);w.Write((byte)2);return m.ToArray();}}
  static void BroadcastChecks(){
   string root=Path.Combine(Path.GetTempPath(),"rx-broadcast-"+Guid.NewGuid().ToString("N"));string cache=Path.Combine(root,"templates.json");
   try{
    Check(object.ReferenceEquals(BroadcastTemplates.Load(cache),BroadcastTemplates.Defaults),"missing broadcast cache uses built-in templates");
    var parsed=BroadcastTemplates.Parse("[\"喵~❤\\n第二行♥\"]");Check(parsed.Length==1&&parsed[0]=="喵~❤\n第二行♥","broadcast JSON preserves symbols and multiline text");
    Reject(()=>BroadcastTemplates.Parse("{\"error\":\"not found\"}"),"broadcast endpoint must return an array");
    Reject(()=>BroadcastTemplates.Parse("[null]"),"null broadcast cannot replace the cache");
    Reject(()=>BroadcastTemplates.Parse("[123]"),"non-text broadcast is rejected");
    Reject(()=>BroadcastTemplates.Parse("[\" \" ]"),"invalid broadcast JSON is rejected");
    Reject(()=>BroadcastTemplates.Parse(Json.Write(new[]{new string('x',4001)})),"oversized broadcast is rejected");
    int calls=0;var result=BroadcastTemplates.Refresh(cache,url=>{calls++;if(url==BroadcastTemplates.MirrorUrl)return "<html>error</html>";return "[\"最新广播❤\"]";});
    Check(calls==2&&result.Cached&&result.Templates[0]=="最新广播❤"&&BroadcastTemplates.Load(cache)[0]=="最新广播❤","invalid Gitee response falls back to GitHub and caches valid templates");
    calls=0;result=BroadcastTemplates.Refresh(cache,url=>{calls++;return "[\"镜像更新♥\"]";});
    Check(calls==1&&result.Cached&&BroadcastTemplates.Load(cache)[0]=="镜像更新♥","valid mirror refresh replaces an existing cache without a GitHub request");
    string before=File.ReadAllText(cache);Reject(()=>BroadcastTemplates.Refresh(cache,url=>{throw new IOException("offline");}),"offline broadcast sources report failure");
    Check(File.ReadAllText(cache)==before,"failed refresh preserves the last valid local cache");
    File.WriteAllText(cache,"broken");Check(object.ReferenceEquals(BroadcastTemplates.Load(cache),BroadcastTemplates.Defaults),"corrupt local cache falls back to built-in templates");
    Check(BroadcastTemplates.Parse("[]").Length==0,"empty remote array can clear all broadcasts");
   }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
  }
  static void SingleInstanceChecks(){
   string name="Local\\RxToolbox.Test."+Guid.NewGuid().ToString("N");
   using(var owner=new System.Threading.Mutex(false,name))using(var contender=new System.Threading.Mutex(false,name)){
    Check(SingleInstance.TryAcquire(owner),"first instance acquires its mutex");
    Check(!Task.Run(()=>SingleInstance.TryAcquire(contender)).Result,"duplicate instance cannot acquire an owned mutex");
    owner.ReleaseMutex();Check(SingleInstance.TryAcquire(contender),"replacement acquires the existing mutex after its owner exits");contender.ReleaseMutex();
   }
   using(var abandoned=new System.Threading.Mutex(false)){
    var thread=new System.Threading.Thread(()=>abandoned.WaitOne());thread.Start();thread.Join();
    Check(SingleInstance.TryAcquire(abandoned),"replacement can recover an abandoned instance mutex");abandoned.ReleaseMutex();
   }
   Check(!SingleInstance.IsApplication(System.Reflection.Assembly.GetExecutingAssembly().Location),"replacement rejects another assembly even with shared product metadata");
  }
  static byte[] PlayerPacket(float duration){using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
   writer.Write(-1);writer.Write((byte)0x44);writer.Write((byte)2);
   writer.Write((byte)5);writer.Write(Encoding.UTF8.GetBytes("玩家❤"));writer.Write((byte)0);writer.Write(-3);writer.Write(duration);
   writer.Write((byte)9);writer.Write((byte)0);writer.Write(42);writer.Write(0f);return stream.ToArray();
  }}
  static void PlayerQueryChecks(){
   var data=A2s.ParsePlayers(PlayerPacket(3661.25f));Check(data.Length==2&&data[0].Name=="玩家❤"&&data[0].Score==-3&&data[0].DurationSeconds==3661.25f&&data[1].Name==""&&data[1].Index==9,"A2S player parser preserves UTF-8 names, scores, indices and session duration");
   Reject(()=>A2s.ParsePlayers(PlayerPacket(1).Take(12).ToArray()),"truncated A2S player packet is rejected");
   foreach(float invalid in new[]{-1f,float.NaN,float.PositiveInfinity,float.MaxValue})Reject(()=>A2s.ParsePlayers(PlayerPacket(invalid)),"invalid player duration is rejected");
   Check(ServerDetailsModule.Duration(90061)=="25:01:01","player duration preserves hours beyond one day");
   var order=new[]{new A2sPlayer{Name="short",DurationSeconds=1},new A2sPlayer{Name="long",DurationSeconds=7200},new A2sPlayer{Name="same",DurationSeconds=7200}};
   var sorted=ServerDetailsModule.SortPlayers(order);Check(sorted[0].Name=="long"&&sorted[1].Name=="same"&&sorted[2].Name=="short"&&order[0].Name=="short","player list sorts by session duration descending and keeps ties stable without modifying the snapshot");
   Check(ServerDetailsService.ParseEndpoint("127.0.0.1:27015").Port==27015&&ServerDetailsService.ParseEndpoint("[::1]:27016").Host=="::1","server details accepts IPv4 and bracketed IPv6 endpoints");
   foreach(string invalid in new[]{"127.0.0.1","127.0.0.1:0","127.0.0.1:70000","name:27015","user@127.0.0.1:27015","127.0.0.1:27015/path"})Reject(()=>ServerDetailsService.ParseEndpoint(invalid),"invalid server details endpoint is rejected");
   using(var udp=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))){
    udp.Client.ReceiveTimeout=4000;int port=((IPEndPoint)udp.Client.LocalEndPoint).Port;
    var fixture=Task.Run(()=>{
     IPEndPoint remote=null;var request=udp.Receive(ref remote);if(request.Length!=9||request[4]!=0x55||request[5]!=255)throw new Exception("invalid initial player query");
     byte[] challenge={255,255,255,255,0x41,1,2,3,4};udp.Send(challenge,challenge.Length,remote);
     request=udp.Receive(ref remote);if(request.Length!=9||request[4]!=0x55||!request.Skip(5).SequenceEqual(new byte[]{1,2,3,4}))throw new Exception("player challenge missing");
     byte[] payload=PlayerPacket(3600);int half=payload.Length/2;
     for(int index=1;index>=0;index--)using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
      writer.Write(-2);writer.Write(321);writer.Write((byte)2);writer.Write((byte)index);writer.Write((ushort)1248);writer.Write(payload,index==0?0:half,index==0?half:payload.Length-half);byte[] packet=stream.ToArray();udp.Send(packet,packet.Length,remote);
     }
    });
    data=A2s.QueryPlayers("127.0.0.1",port);fixture.GetAwaiter().GetResult();Check(data.Length==2&&data[0].DurationSeconds==3600,"A2S player query negotiates a challenge and reassembles out-of-order UDP fragments");
   }
   byte[] basic=InfoPacket();using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
    writer.Write(basic);writer.Write((byte)'d');writer.Write((byte)'l');writer.Write((byte)0);writer.Write((byte)1);writer.Write(Encoding.UTF8.GetBytes("2026.11.15"));writer.Write((byte)0);writer.Write((byte)0x81);writer.Write((ushort)27015);writer.Write((ulong)4000);
    var info=A2s.ParseInfo(stream.ToArray());Check(info.Folder=="garrysmod"&&info.AppId==4000&&info.System=="Linux"&&info.Vac==true&&info.Password==false&&info.Version=="2026.11.15","extended server info parses game folder, AppID, OS, VAC, password, version and extra-data fields");
   }
  }
  static async Task ServerDetailsChecks(){
   var time=new DateTime(2026,11,15,0,0,0,DateTimeKind.Utc);int infoCalls=0,playerCalls=0;bool failInfo=false,failPlayers=false;
   var first=new ServerEntry{Host="127.0.0.1",Port=27015};var second=new ServerEntry{Host="127.0.0.2",Port=27015};
   using(var service=new ServerDetailsService((host,port)=>{infoCalls++;if(failInfo)throw new IOException();return new A2sInfo{Name=host,Map="test"};},(host,port)=>{playerCalls++;if(failPlayers)throw new IOException();return new[]{new A2sPlayer{Name=host,DurationSeconds=100}};},()=>time)){
    await service.PollAsync();Check(infoCalls==0&&playerCalls==0,"server details sends no query before a server is selected");
    service.Select(first);await service.PollAsync();await service.PollAsync();service.Select(first);await service.PollAsync();Check(infoCalls==1&&playerCalls==1&&service.NextRefreshSeconds==30,"repeated selection and polling share a 30-second per-server cooldown");
    time=time.AddSeconds(29);await service.PollAsync();Check(infoCalls==1,"server details does not refresh before 30 seconds");
    time=time.AddSeconds(1);await service.PollAsync();Check(infoCalls==2&&playerCalls==2,"server details refreshes both info and players at 30 seconds");
    service.Select(second);await service.PollAsync();service.Select(first);await service.PollAsync();Check(infoCalls==3&&service.Snapshot.Info.Name==first.Host,"switching back uses that server's own cached data and cooldown");
    var cachedPlayers=service.Snapshot.Players;time=time.AddSeconds(30);failPlayers=true;await service.PollAsync();Check(service.Snapshot.InfoError==null&&service.Snapshot.PlayersError!=null&&object.ReferenceEquals(cachedPlayers,service.Snapshot.Players),"player-query failure retains its last successful list while updating server info");
    var cachedInfo=service.Snapshot.Info;failInfo=true;time=time.AddSeconds(30);await service.PollAsync();int attempts=infoCalls;await service.PollAsync();Check(service.Snapshot.InfoError!=null&&object.ReferenceEquals(cachedInfo,service.Snapshot.Info)&&attempts==infoCalls,"failed queries retain cached info and do not cause immediate retries");
   }
   using(var release=new System.Threading.ManualResetEventSlim(false)){
    var entered=new TaskCompletionSource<bool>();int queried=0;
    using(var service=new ServerDetailsService((host,port)=>{if(++queried==1){entered.SetResult(true);if(!release.Wait(5000))throw new TimeoutException();}return new A2sInfo{Name=host};},(host,port)=>new A2sPlayer[0],()=>time)){
     service.Select(first);var pending=service.PollAsync();await entered.Task;service.Select(second);await service.PollAsync();Check(queried==1,"switching servers does not overlap an in-flight details query");release.Set();await pending;
     Check(service.Snapshot.Info==null,"an old response cannot populate the newly selected server");await service.PollAsync();Check(service.Snapshot.Info.Name==second.Host&&queried==2,"newly selected server is queried after the previous query finishes");
    }
   }
   using(var release=new System.Threading.ManualResetEventSlim(false)){
    var entered=new TaskCompletionSource<bool>();int playerQueries=0;
    var service=new ServerDetailsService((host,port)=>{entered.SetResult(true);release.Wait(5000);return new A2sInfo();},(host,port)=>{playerQueries++;return new A2sPlayer[0];},()=>time);
    service.Select(first);var pending=service.PollAsync();await entered.Task;service.Dispose();release.Set();await pending;Check(playerQueries==0&&service.Snapshot.Info==null,"disposing details service prevents late UI data and further player requests");
   }
  }
  [STAThread]public static int Main(){try{
   PlayerQueryChecks();Task.Run((Func<Task>)ServerDetailsChecks).GetAwaiter().GetResult();
   SingleInstanceChecks();
   PrivacyChecks();
   BroadcastChecks();
   NvidiaChecks();
   var cs=CrosshairImport.Parse("CSvbPubOq37zTGqtsPTP5QTrp5CB4xFXiKRLfzJsm49ZRe");Check(cs.Red==255&&cs.Green==0&&cs.Blue==0&&cs.Length==5&&cs.Thickness==1&&cs.Dot&&cs.ReferenceHeight==768,"current CS2 reference vector");
   var old=CrosshairImport.Parse("CSGO-Cn37R-YE7vo-pLCAL-aURmZ-z6zkG");Check(old.Source.Contains("旧版"),"legacy CSGO reference vector");
   foreach(int version in new[]{3,4}){byte[] b=new byte[18];b[1]=(byte)version;b[2]=68;b[3]=20;b[4]=200;b[7]=3;b[8]=7;b[12]=128;b[14]=56;b[15]=4;var p=CrosshairImport.Parse(Encode(b,true));Check(p.Length==7&&p.Thickness==1&&p.ReferenceHeight==1080&&p.Dot,"legacy pixel v"+version);}
   byte[] signed=new byte[32];signed[1]=1;signed[4]=4;signed[13]=2;signed[14]=254;signed[15]=255;signed[16]=6;Check(CrosshairImport.Parse(Encode(signed,false)).Gap==-2,"signed CS2 gap");
   Reject(()=>CrosshairImport.Parse("CSvbPubOq37zTGqtsPTP5QTrp5CB4xFXiKRLfzJsm49ZRf"),"checksum corruption");Reject(()=>CrosshairImport.Parse("CSGO-AAAAA-AAAAA-AAAAA-AAAAA-AAAAg"),"invalid base57 alphabet");
   var val=CrosshairImport.Parse("0;s;1;P;c;5;h;0;0l;4;0o;2;0a;1;0f;0;1b;0;A;c;7;0l;20;S;c;4;o;1");Check(val.Red==0&&val.Green==255&&val.Blue==255&&val.Length==4&&val.Gap==2&&!val.Outer&&val.Outline==0,"VALORANT P section isolated from ADS and scope");
   var sample=CrosshairImport.Parse("0;P;h;0;f;0;0t;1;0l;4;0o;1;0a;1;0f;0;1t;3;1o;2;1a;1;1m;0;1f;0");Check(sample.Length==4&&sample.Thickness==1&&sample.Gap==1&&sample.OuterThickness==3&&sample.OuterGap==2,"user VALORANT code retains independent inner and outer geometry");
   using(var raster=new Bitmap(200,200))using(var graphics=Graphics.FromImage(raster)){CrosshairPainter.Draw(graphics,sample,100,100,1);Check(raster.GetPixel(103,99).A==raster.GetPixel(103,101).A&&raster.GetPixel(103,101).A>0,"odd outer line thickness stays centered on the same pixel as inner lines");}
   Check(new Settings().CrosshairScale==2,"default crosshair display scale is two without modifying imported geometry");
   var custom=CrosshairImport.Parse("0;P;c;8;u;12AB34FF;0l;7;0v;3;0g;1");Check(custom.Red==18&&custom.Green==171&&custom.VerticalLength==3,"VALORANT custom color and unlinked length");
   Reject(()=>CrosshairImport.Parse("0;P;0l;NaN"),"nonfinite values");Reject(()=>CrosshairImport.Parse("0;P;0l"),"truncated VALORANT pair");Reject(()=>CrosshairImport.Parse("0;P;0l;3;0l;4"),"duplicate VALORANT fields");
   long now=(long)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;
   var round=RoundSnapshot.Create(new ApiServer{round_time_left=923,rounds_left=0,updated_at=now-3},now,DateTime.UtcNow);Check(round.Remaining(0)<=920&&round.Remaining(0)>=919&&!round.Stale,"countdown compensates sample age");Check(round.Data.rounds_left==0,"zero remaining rounds preserved");
   round=RoundSnapshot.Create(new ApiServer{round_time_left=923,updated_at=now-100},now,DateTime.UtcNow);Check(round.Stale&&round.Remaining(0)==-1,"stale snapshot never displays live time");
   round=RoundSnapshot.Create(new ApiServer{round_time_left=-1,updated_at=now},now,DateTime.UtcNow);Check(round.Remaining(30)==-1,"idle sentinel preserved");
   var env=Json.Read<ApiEnvelope>("{\"server_time\":1794471201,\"servers\":[{\"ip\":\"127.0.0.1\",\"port\":27076,\"rounds_left\":1,\"round_time_left\":923,\"round_phase\":\"回合中\",\"updated_at\":1794471201}]}");Check(env.servers[0].round_time_left==923,"actual API schema");
   var info=A2s.ParseInfo(InfoPacket());Check(info.Name=="测试服务器"&&info.Players==12&&info.MaxPlayers==64&&info.Map=="rx_site","A2S UTF-8 response parser");Reject(()=>A2s.ParseInfo(new byte[]{255,255,255,255,0x49}),"truncated A2S packet");
   UdpFixture();
   Reject(()=>Updates.Validate(new UpdateManifest{version="1.1.0",url="https://example.com/app.exe",sha256="wrong"}),"update checksum mandatory");Reject(()=>HttpData.Https("http://example.com"),"reject insecure update source");
   Check(Updates.ParseVersion("v1.10.0")>Updates.ParseVersion("1.9.9"),"semantic version comparison");Check(Updates.Hash(Encoding.ASCII.GetBytes("abc"))=="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad","SHA256 known vector");
   byte[] exe=File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"RxToolbox.exe"));var manifest=new UpdateManifest{version=Updates.Current.ToString(3),url="https://example.com/primary.exe",mirrors=new[]{"https://mirror.example.com/app.exe"},sha256=Updates.Hash(exe)};
   int requests=0;var checkedManifest=Updates.CheckCore("owner/repo","https://mirror.example.com/update.json",url=>{requests++;if(requests==1)throw new IOException("offline");return Json.Write(manifest);});Check(requests==2&&checkedManifest.version==Updates.Current.ToString(3),"manifest fallback from mirror to GitHub");
   requests=0;string staged=Updates.DownloadCore(manifest,url=>{requests++;return requests==1?new byte[]{0}:exe;});Check(requests==2&&File.ReadAllBytes(staged).SequenceEqual(exe),"download fallback after corrupt primary and assembly identity check");File.Delete(staged);Directory.Delete(Path.GetDirectoryName(staged));
   manifest.version=new Version(Updates.Current.Major+1,0,0).ToString(3);Reject(()=>Updates.DownloadCore(manifest,url=>exe),"assembly version mismatch rejected");
   using(var bitmap=new Bitmap(200,200))using(var g=Graphics.FromImage(bitmap)){CrosshairPainter.Draw(g,new CrosshairProfile(),100,100,1);Check(bitmap.GetPixel(105,100).A>0&&bitmap.GetPixel(100,100).A==0,"crosshair raster and center gap");}
   using(var field=new NumberField(0,-300,300,1)){
    int changes=0;field.ValueChanged+=delegate{changes++;};var text=field.Controls.OfType<System.Windows.Forms.TextBox>().Single();
    text.Text="-301";field.Commit();Check(field.Value==-300&&changes==1,"numeric input clamps negative compensation to minimum");
    text.Text="invalid";field.Commit();Check(field.Value==-300&&text.Text=="-300"&&changes==1,"invalid numeric input restores last valid value");
    text.Text="23";field.Commit();Check(field.Value==23&&changes==2,"typed compensation commits once");
    field.Value=999;Check(field.Value==300,"numeric upper limit enforced");
   }
   using(var field=new NumberField(1,.25m,4,.05m)){var text=field.Controls.OfType<System.Windows.Forms.TextBox>().Single();text.Text=1.235m.ToString(System.Globalization.CultureInfo.CurrentCulture);field.Commit();Check(field.Value==1.24m,"fractional scale rounds to two decimal places");}
   var appearance=new TimerAppearance{Width=-1,Height=9000,Opacity=0,Parts=null};appearance.Validate();Check(appearance.Width==240&&appearance.Height==600&&appearance.Opacity==0&&appearance.Parts.Length==4,"timer layout validates sizes and restores missing components");
   appearance=new TimerAppearance();var draft=appearance.Clone();draft.Parts[1].X=48;Check(appearance.Parts[1].X==14,"timer editor draft does not mutate saved layout");
   draft.Parts[1].X=9999;draft.Parts[1].Y=-20;draft.Parts[1].FontSize=float.NaN;draft.Validate();Check(draft.Parts[1].X+draft.Parts[1].Width<=draft.Width&&draft.Parts[1].Y==0&&draft.Parts[1].FontSize==12,"timer components stay within canvas and reject invalid font size");
   appearance=new TimerAppearance{Background=System.Drawing.Color.Black.ToArgb(),Accent=System.Drawing.Color.White.ToArgb(),PhaseColor=false};
   foreach(var part in appearance.Parts)part.Visible=part.Id=="time";
   using(var bitmap=TimerRenderer.Render(appearance,TimerContent.Sample)){var bounds=appearance.Parts[1].Bounds;int top=bitmap.Height,bottom=0;for(int by=bounds.Top;by<bounds.Bottom;by++)for(int bx=bounds.Left;bx<bounds.Right;bx++){var pixel=bitmap.GetPixel(bx,by);if(pixel.A>240&&pixel.R>200){top=Math.Min(top,by);bottom=Math.Max(bottom,by);}}Check(top<bottom&&Math.Abs((top+bottom)/2.0-(bounds.Top+bounds.Height/2.0))<7,"countdown glyphs are vertically centered within component");}
   using(var timerBitmap=TimerRenderer.Render(new TimerAppearance(),TimerContent.Sample))Check(Math.Abs(timerBitmap.HorizontalResolution-96)<.1&&Math.Abs(timerBitmap.VerticalResolution-96)<.1,"timer rendering uses fixed DPI so high-DPI displays cannot clip digits");
   var timerSettings=new Settings();using(var service=new RoundService(timerSettings)){timerSettings.SelectedServer=service.Servers[0].Key;timerSettings.TimerServer=service.Servers[1].Key;Check(service.TimerSelected==service.Servers[1]&&service.Selected==service.Servers[0],"overlay server selection is independent of main list");timerSettings.TimerServer="";Check(service.TimerSelected==service.Selected,"overlay can follow the main server selection");timerSettings.TimerServer="missing";Check(service.TimerSelected==service.Selected,"missing overlay server falls back to current main selection");}
   var transparentTimer=new TimerAppearance{Opacity=0};using(var transparent=TimerRenderer.Render(transparentTimer,TimerContent.Sample)){
    Check(transparent.GetPixel(transparent.Width-20,transparent.Height-10).A==0,"zero background opacity leaves empty areas fully transparent");
    var area=transparentTimer.Parts[1].Bounds;int opaqueText=0;for(int ty=area.Top;ty<area.Bottom;ty++)for(int tx=area.Left;tx<area.Right;tx++)if(transparent.GetPixel(tx,ty).A==255)opaqueText++;
    Check(opaqueText>100,"zero background opacity preserves opaque countdown glyphs");
   }
   transparentTimer.Opacity=128;using(var translucent=TimerRenderer.Render(transparentTimer,TimerContent.Sample))Check(translucent.GetPixel(transparentTimer.Width-20,transparentTimer.Height-10).A==128,"background pixels use configured alpha independently of text");
   Task.Run((Func<Task>)AutoJoinChecks).GetAwaiter().GetResult();
   using(var checkbox=new FluentCheck{Text="核弹红色",Size=new Size(180,36),Checked=true})using(var bitmap=new Bitmap(180,36)){
    using(var g=Graphics.FromImage(bitmap))g.Clear(Color.Magenta);
    checkbox.DrawToBitmap(bitmap,new Rectangle(0,0,180,36));
    Check(bitmap.GetPixel(179,18).ToArgb()==Ui.Card.ToArgb(),"checkbox repaints entire surface instead of retaining hovered button pixels");
    int changes=0;checkbox.CheckedChanged+=delegate{changes++;};checkbox.AccessibilityObject.DoDefaultAction();Check(!checkbox.Checked&&changes==1,"custom checkbox accessibility action toggles state exactly once");
   }
   var oldSettings=new Settings{GithubRepo="",MirrorUrl=""};Updates.UseDefaultSource(oldSettings);Check(oldSettings.GithubRepo=="yushy123416/rxsend-tool"&&oldSettings.MirrorUrl==Updates.DefaultMirrorUrl,"legacy empty update source migrates to Gitee primary and GitHub fallback");
   var githubSettings=new Settings{GithubRepo=Updates.DefaultRepository,MirrorUrl=""};Updates.UseDefaultSource(githubSettings);Check(githubSettings.MirrorUrl==Updates.DefaultMirrorUrl,"existing default GitHub settings gain Gitee primary mirror");
   var mirrorSettings=new Settings{GithubRepo="",MirrorUrl="https://example.com/update.json"};Updates.UseDefaultSource(mirrorSettings);Check(mirrorSettings.GithubRepo==""&&mirrorSettings.MirrorUrl=="https://example.com/update.json","custom mirror configuration is preserved");
   var window=new MainWindow();window.Dispose();window.Dispose();Check(window.IsDisposed,"main window can be disposed twice without tray null reference");
   Console.WriteLine(count+" checks passed");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
  static void UdpFixture(){using(var server=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))){server.Client.ReceiveTimeout=4000;int port=((IPEndPoint)server.Client.LocalEndPoint).Port;var task=Task.Run(delegate{IPEndPoint remote=null;byte[] first=server.Receive(ref remote);if(first.Length!=25||first[4]!=0x54)throw new Exception("wrong info request");byte[] challenge={255,255,255,255,65,11,22,33,44};server.Send(challenge,challenge.Length,remote);byte[] second=server.Receive(ref remote);if(second.Length!=29||second[25]!=11)throw new Exception("missing challenge");var packet=InfoPacket();int half=packet.Length/2;for(int part=1;part>=0;part--){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(-2);w.Write(123);w.Write((byte)2);w.Write((byte)part);w.Write((ushort)1248);int start=part==0?0:half;w.Write(packet,start,part==0?half:packet.Length-half);var bytes=m.ToArray();server.Send(bytes,bytes.Length,remote);}}});var info=A2s.QueryInfo("127.0.0.1",port);task.GetAwaiter().GetResult();Check(info.Players==12,"A2S challenge and out-of-order split packets");}}
 }
}
