using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Temporary asset import and focused native verification. Archived after use.
[InitializeOnLoad]
public static class HouseRuinsProbe
{
    const string Art="Assets/Art/HouseRuins/", Output="Backups/HouseRuins/";
    static HouseRuinsProbe(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string path="Temp/HouseRuins/request";
        if(EditorApplication.isCompiling||!File.Exists(path))return;
        string action;try{action=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try
        {
            if(action=="import")Import();
            else if(action=="baseline"||action=="verify")Verify(action=="baseline");
            else if(action=="live"||action=="next")Live(action=="next");
            else if(action=="metrics")Metrics();
            else if(action=="capture")
            {
                if(!EditorApplication.isPlaying)throw new Exception("Capture requires Play Mode.");
                ScreenCapture.CaptureScreenshot(Output+"gameplay.png");
            }
        }
        catch(Exception e){File.WriteAllText(Output+"error-"+action+".txt",e.ToString());}
    }
    static void Check(ISpriteEditorDataProvider provider)
    {
        var c=provider.GetDataProvider<ISpriteFrameEditCapability>();
        if(c==null)throw new Exception("No Sprite Editor edit capability.");
        var flags=c.GetEditCapability();
        foreach(var flag in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditSpriteName,EEditCapability.EditSpriteRect,EEditCapability.EditPivot})
            if(!flags.HasCapability(flag))throw new Exception("Unsupported sprite capability: "+flag);
    }
    static ISpriteEditorDataProvider Provider(TextureImporter importer)
    {
        var factory=new SpriteDataProviderFactories();factory.Init();
        var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();Check(provider);return provider;
    }
    static TextureImporter Configure(string path,float ppu)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(importer==null)throw new Exception("No texture importer: "+path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=ppu;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=2048;importer.wrapMode=TextureWrapMode.Clamp;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.Tight;settings.spriteGenerateFallbackPhysicsShape=false;importer.SetTextureSettings(settings);
        return importer;
    }
    static void Apply(ISpriteEditorDataProvider provider,TextureImporter importer,List<SpriteRect> rects)
    {
        provider.SetSpriteRects(rects.ToArray());
        var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if(names==null)throw new Exception("Missing sprite name provider.");
        names.SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply();importer.SaveAndReimport();
    }
    static GUID Stable(ISpriteEditorDataProvider provider,string name)
    {
        var old=provider.GetSpriteRects().FirstOrDefault(r=>r.name==name);return old!=null?old.spriteID:GUID.Generate();
    }
    static Texture2D Read(string path)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if(!texture.LoadImage(File.ReadAllBytes(path)))throw new Exception("Invalid PNG.");return texture;
    }
    static void Materials()
    {
        string path=Art+"HouseMaterials.png";var image=Read(path);
        int half=image.width/2,cell=Math.Min(image.width,image.height)/8;
        var importer=Configure(path,cell);var provider=Provider(importer);
        var rects=new List<SpriteRect>();string[] names={"Stone","Timber","Wallpaper","Plaster"};
        for(int material=0;material<4;material++)for(int y=0;y<4;y++)for(int x=0;x<4;x++)
        {
            string name=names[material]+"_"+(x+y*4).ToString("00");
            rects.Add(new SpriteRect{name=name,spriteID=Stable(provider,name),alignment=SpriteAlignment.Center,pivot=Vector2.one*.5f,
                rect=new Rect((material%2)*half+x*cell,(material<2?image.height/2:0)+y*cell,cell,cell)});
        }
        Apply(provider,importer,rects);UnityEngine.Object.DestroyImmediate(image);
    }
    // Import a tight render mesh around substantial high-alpha artwork. PNGs stay untouched.
    static List<List<Vector2Int>> Parts(Color32[] pixels,int imageWidth,RectInt cell)
    {
        int w=cell.width,h=cell.height;var visited=new bool[w*h];var parts=new List<List<Vector2Int>>();
        for(int start=0;start<visited.Length;start++)
        {
            if(visited[start])continue;visited[start]=true;
            if(pixels[(cell.y+start/w)*imageWidth+cell.x+start%w].a<220)continue;
            var part=new List<Vector2Int>();var queue=new Queue<int>();queue.Enqueue(start);
            while(queue.Count>0)
            {
                int n=queue.Dequeue(),x=n%w,y=n/w;part.Add(new Vector2Int(cell.x+x,cell.y+y));
                foreach(var d in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                {
                    int xx=x+d.x,yy=y+d.y;if(xx<0||xx>=w||yy<0||yy>=h)continue;
                    int next=yy*w+xx;if(visited[next])continue;visited[next]=true;
                    if(pixels[(cell.y+yy)*imageWidth+cell.x+xx].a>=220)queue.Enqueue(next);
                }
            }
            parts.Add(part);
        }
        int maximum=parts.Max(p=>p.Count);
        return parts.Where(p=>p.Count>=Math.Max(80,maximum*.01f)).ToList();
    }
    static Vector2[] Outline(List<Vector2Int> pixels,Rect rect)
    {
        var rows=pixels.GroupBy(p=>p.y).OrderBy(r=>r.Key).ToArray();var outline=new List<Vector2>();
        foreach(var row in rows)outline.Add(new Vector2(row.Min(p=>p.x)-.5f,row.Key)-rect.center);
        foreach(var row in rows.Reverse())outline.Add(new Vector2(row.Max(p=>p.x)+1.5f,row.Key+1)-rect.center);
        var simplified=new List<Vector2>();
        for(int i=0;i<outline.Count;i++)
        {
            Vector2 a=outline[(i+outline.Count-1)%outline.Count],b=outline[i],c=outline[(i+1)%outline.Count];
            if(Mathf.Abs((b.x-a.x)*(c.y-b.y)-(b.y-a.y)*(c.x-b.x))>.01f)simplified.Add(b);
        }
        return simplified.ToArray();
    }
    static void Props()
    {
        string path=Art+"HouseProps.png";var image=Read(path);var pixels=image.GetPixels32();
        var importer=Configure(path,256);var provider=Provider(importer);var rects=new List<SpriteRect>();
        var outlines=new List<List<Vector2[]>>();
        string[] names={"Bed","SealedDoor","Window","EmptyFrame","WarmCorner","Shelf","Ivy","Braces"};
        for(int i=0;i<8;i++)
        {
            int x=i%4,y=1-i/4;
            var cell=new RectInt(x*image.width/4,y*image.height/2,(x+1)*image.width/4-x*image.width/4,(y+1)*image.height/2-y*image.height/2);
            var parts=Parts(pixels,image.width,cell);var all=parts.SelectMany(p=>p).ToArray();
            int minX=Math.Max(cell.xMin,all.Min(p=>p.x)-2),minY=Math.Max(cell.yMin,all.Min(p=>p.y)-2);
            int maxX=Math.Min(cell.xMax,all.Max(p=>p.x)+3),maxY=Math.Min(cell.yMax,all.Max(p=>p.y)+3);
            var rect=new Rect(minX,minY,maxX-minX,maxY-minY);
            rects.Add(new SpriteRect{name=names[i],spriteID=Stable(provider,names[i]),alignment=SpriteAlignment.BottomCenter,pivot=new Vector2(.5f,0),rect=rect});
            outlines.Add(parts.Select(p=>Outline(p,rect)).ToList());
        }
        provider.SetSpriteRects(rects.ToArray());
        var meshes=provider.GetDataProvider<ISpriteOutlineDataProvider>();
        if(meshes==null)throw new Exception("Missing render outline provider.");
        for(int i=0;i<8;i++){meshes.SetOutlines(rects[i].spriteID,outlines[i]);meshes.SetTessellationDetail(rects[i].spriteID,.25f);}
        Apply(provider,importer,rects);UnityEngine.Object.DestroyImmediate(image);
    }
    static TileBase[] Tiles(string material)
    {
        string directory=Art+"Tiles";Directory.CreateDirectory(directory);var result=new TileBase[16];
        var sprites=AssetDatabase.LoadAllAssetsAtPath(Art+"HouseMaterials.png").OfType<Sprite>().ToDictionary(s=>s.name);
        for(int i=0;i<16;i++)
        {
            string name=material+"_"+i.ToString("00"),path=directory+"/"+name+".asset";
            var tile=AssetDatabase.LoadAssetAtPath<Tile>(path);
            if(tile==null){tile=ScriptableObject.CreateInstance<Tile>();AssetDatabase.CreateAsset(tile,path);}
            tile.name=name;tile.sprite=sprites[name];tile.colliderType=Tile.ColliderType.None;tile.color=Color.white;tile.transform=Matrix4x4.identity;
            EditorUtility.SetDirty(tile);result[i]=tile;
        }
        return result;
    }
    static void Import()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play before importing.");
        Materials();Props();AssetDatabase.Refresh();
        string path="Assets/Resources/RuinedHouseTheme.asset";
        var theme=AssetDatabase.LoadAssetAtPath<RuinedHouseTheme>(path);
        if(theme==null){theme=ScriptableObject.CreateInstance<RuinedHouseTheme>();AssetDatabase.CreateAsset(theme,path);}
        theme.stone=Tiles("Stone");theme.timber=Tiles("Timber");theme.wallpaper=Tiles("Wallpaper");theme.plaster=Tiles("Plaster");
        var props=AssetDatabase.LoadAllAssetsAtPath(Art+"HouseProps.png").OfType<Sprite>().ToDictionary(s=>s.name);
        theme.props=new[]{"Bed","SealedDoor","Window","EmptyFrame","WarmCorner","Shelf","Ivy","Braces"}.Select(n=>props[n]).ToArray();
        EditorUtility.SetDirty(theme);AssetDatabase.SaveAssets();
        File.WriteAllText(Output+"import.json","{\"materialSprites\":64,\"props\":8,\"tiles\":64,\"colliderFree\":true,\"spriteProviderCapabilitiesPassed\":true}");
    }
    static void Metrics()
    {
        var lines=new List<string>();var image=Read(Art+"HouseProps.png");var pixels=image.GetPixels32();
        foreach(var s in Resources.Load<RuinedHouseTheme>("RuinedHouseTheme").props)
        {
            int bottom=image.height,top=0,left=image.width,right=0;var r=s.rect;
            for(int y=(int)r.yMin;y<(int)r.yMax;y++)for(int x=(int)r.xMin;x<(int)r.xMax;x++)
                if(pixels[y*image.width+x].a>=220){bottom=Math.Min(bottom,y);top=Math.Max(top,y);left=Math.Min(left,x);right=Math.Max(right,x);}
            lines.Add(s.name+" rect="+r+" pivot="+s.pivot+" bounds="+s.bounds+" meshY="+s.vertices.Min(v=>v.y)+".."+s.vertices.Max(v=>v.y)+" opaqueY="+bottom+".."+top+" opaqueX="+left+".."+right);
        }
        UnityEngine.Object.DestroyImmediate(image);File.WriteAllLines(Output+"sprite-metrics.txt",lines);
    }
    [Serializable] public class WorldCheck {public int seed,solidCells,rooms,props,paths;public float maximumFurnitureGap;public string error;public bool furnitureGrounded,originalTilesPreserved,collisionPathsIdentical,skinMatchesOccupancy,skinWorldCoordinates,noDecorativeColliders,worldUnchanged,repeatStable,roomLayoutStable,oldArtworkRetired;}
    [Serializable] public class Report {public string error;public bool ready;public List<WorldCheck> worlds=new List<WorldCheck>();}
    static Tilemap MakeMap(Transform grid)
    {
        var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tilemap.prefab"));obj.transform.SetParent(grid,false);return obj.GetComponent<Tilemap>();
    }
    static void OriginalApply(WorldData world,Tilemap map,TileBase tile)
    {
        int w=world.Tiles.GetLength(0),h=world.Tiles.GetLength(1);map.transform.localScale=Vector3.one*(float)world.CellSize;
        var cells=new TileBase[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(world.Tiles[x,y]==1)cells[x+y*w]=tile;
        map.ClearAllTiles();map.SetTilesBlock(new BoundsInt(0,0,0,w,h,1),cells);map.RefreshAllTiles();
        map.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();map.GetComponent<CompositeCollider2D>().GenerateGeometry();Physics2D.SyncTransforms();
    }
    static bool SamePaths(CompositeCollider2D a,CompositeCollider2D b)
    {
        if(a.pathCount!=b.pathCount||a.pointCount!=b.pointCount)return false;
        for(int p=0;p<a.pathCount;p++)
        {
            int count=a.GetPathPointCount(p);if(count!=b.GetPathPointCount(p))return false;
            var aa=new Vector2[count];var bb=new Vector2[count];a.GetPath(p,aa);b.GetPath(p,bb);
            for(int i=0;i<count;i++)if((aa[i]-bb[i]).sqrMagnitude>.00000001f)return false;
        }
        return true;
    }
    static string SkinSignature(Tilemap map)
    {
        ulong hash=14695981039346656037UL;
        unchecked{foreach(var tile in map.GetTilesBlock(map.cellBounds)){if(tile==null)hash*=1099511628211UL;else foreach(char c in tile.name)hash=(hash^c)*1099511628211UL;}}return hash.ToString("X16");
    }
    static string RoomSignature(Transform root)
    {
        string props=string.Join(";",root.GetComponentsInChildren<SpriteRenderer>().Select(s=>s.gameObject.name+":"+s.sprite.name+":"+s.transform.localPosition.ToString("F5")+":"+s.transform.localScale.ToString("F5")));
        return props+SkinSignature(root.Find("Domestic back walls").GetComponent<Tilemap>());
    }
    static void Verify(bool baseline)
    {
        var report=new Report();Scene scene=default(Scene);
        try
        {
            var theme=Resources.Load<RuinedHouseTheme>("RuinedHouseTheme");report.ready=theme!=null&&theme.Ready;
            if(!report.ready)throw new Exception("House theme not imported yet.");
            scene=EditorApplication.isPlaying
                ? SceneManager.CreateScene("House artwork verification",new CreateSceneParameters(LocalPhysicsMode.Physics2D))
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var caps=JsonUtility.FromJson<TraversalCapabilities>(Resources.Load<TextAsset>("TraversalCapabilities").text);
            var source=UnityEngine.Object.FindAnyObjectByType<Player>();
            var original=AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Casillas/SueloAvanzado.asset");
            foreach(int seed in new[]{2,11,42})
            {
                var check=new WorldCheck{seed=seed};report.worlds.Add(check);
                var world=ProceduralWorld.GenerateTraversal(new TraversalGenerationSettings{Layout=TraversalLayout.Random,RequiredFragments=12},caps,source.staminaSettings,seed);
                string before=WorldRunSession.GeometrySignature(world);int fragments=world.FragmentNodes.Length;
                var grid=new GameObject("House verification grid",typeof(Grid));SceneManager.MoveGameObjectToScene(grid,scene);
                var a=MakeMap(grid.transform);var b=MakeMap(grid.transform);OriginalApply(world,a,original);WorldTilemapRenderer.Apply(world,b,original);
                int width=world.Tiles.GetLength(0),height=world.Tiles.GetLength(1);bool same=true,skinSame=true,coordinates=true;
                var root=b.transform.Find("House artwork");var skin=root.Find("House terrain skin").GetComponent<Tilemap>();
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    var pos=new Vector3Int(x,y,0);same&=a.GetTile(pos)==b.GetTile(pos);
                    bool solid=world.Tiles[x,y]==1;skinSame&=skin.HasTile(pos)==solid;
                    coordinates&=(skin.GetCellCenterWorld(pos)-b.GetCellCenterWorld(pos)).sqrMagnitude<.00000001f;
                    if(solid){check.solidCells++;skinSame&=skin.GetSprite(pos)!=null;}
                }
                check.originalTilesPreserved=same;check.skinMatchesOccupancy=skinSame;check.skinWorldCoordinates=coordinates;
                check.collisionPathsIdentical=SamePaths(a.GetComponent<CompositeCollider2D>(),b.GetComponent<CompositeCollider2D>());
                check.paths=b.GetComponent<CompositeCollider2D>().pathCount;
                check.noDecorativeColliders=root.GetComponentsInChildren<Collider2D>(true).Length==0;
                check.props=root.GetComponentsInChildren<SpriteRenderer>().Length;
                check.rooms=root.GetComponentsInChildren<SpriteRenderer>().Select(s=>s.gameObject.name.Split('/')[0]).Distinct().Count();
                var support=b.GetComponent<CompositeCollider2D>();check.furnitureGrounded=true;
                foreach(var prop in root.GetComponentsInChildren<SpriteRenderer>())
                {
                    if(prop.sprite.name!="Bed"&&prop.sprite.name!="SealedDoor"&&prop.sprite.name!="WarmCorner")continue;
                    Vector2 sole=new Vector2(prop.bounds.center.x,prop.bounds.min.y);
                    float gap=Vector2.Distance(sole,support.ClosestPoint(sole));
                    check.maximumFurnitureGap=Mathf.Max(check.maximumFurnitureGap,gap);check.furnitureGrounded&=gap<.02f;
                }
                check.worldUnchanged=before==WorldRunSession.GeometrySignature(world)&&fragments==world.FragmentNodes.Length;
                string first=SkinSignature(skin),firstRooms=RoomSignature(root);WorldTilemapRenderer.Apply(world,b,original);
                check.oldArtworkRetired=root==null||!root.gameObject.activeSelf;
                var rebuilt=b.transform.Cast<Transform>().Single(t=>t.name=="House artwork"&&t.gameObject.activeSelf);
                check.repeatStable=first==SkinSignature(rebuilt.Find("House terrain skin").GetComponent<Tilemap>());
                check.roomLayoutStable=firstRooms==RoomSignature(rebuilt);
                if(!same||!skinSame||!coordinates||!check.collisionPathsIdentical||!check.noDecorativeColliders||!check.worldUnchanged||!check.repeatStable||!check.roomLayoutStable||!check.furnitureGrounded||check.props==0||!check.oldArtworkRetired)
                    throw new Exception("Native geometry/presentation invariant failed for seed "+seed);
                UnityEngine.Object.DestroyImmediate(grid);
            }
        }
        catch(Exception e){report.error=e.ToString();}
        finally{if(scene.IsValid()){if(EditorApplication.isPlaying)SceneManager.UnloadSceneAsync(scene);else EditorSceneManager.CloseScene(scene,true);}}
        File.WriteAllText(Output+(baseline?"before-native.json":"native-regression.json"),JsonUtility.ToJson(report,true));
    }
    [Serializable]public class LiveReport{public string error,scene,signature;public int seed,level,fragments,props,decorativeColliders,activeRoots;public bool playMode,skinPresent,originalTilePreserved,debugVisibility,propsOutsideBackgroundDarkening,oldRootRetired;}
    static void Live(bool next)
    {
        var report=new LiveReport();
        try
        {
            if(!EditorApplication.isPlaying)throw new Exception("Requires Play Mode.");
            var book=UnityEngine.Object.FindAnyObjectByType<Libro>();
            var oldRoot=book.MapaDeCasillas.transform.Find("House artwork");
            if(next)book.Generacion(book.Nivel+1);
            var root=book.MapaDeCasillas.transform.Cast<Transform>().Single(t=>t.name=="House artwork"&&t.gameObject.activeSelf);
            var skin=root.Find("House terrain skin").GetComponent<Tilemap>();
            report.playMode=true;report.scene=SceneManager.GetActiveScene().name;report.level=book.Nivel;report.seed=book.CurrentSeed;report.signature=book.CurrentSignature;
            report.fragments=book.RequiredFragments;report.skinPresent=skin!=null;
            report.props=root.GetComponentsInChildren<SpriteRenderer>().Length;report.decorativeColliders=root.GetComponentsInChildren<Collider2D>().Length;
            report.activeRoots=book.MapaDeCasillas.transform.Cast<Transform>().Count(t=>t.name=="House artwork"&&t.gameObject.activeSelf);
            report.oldRootRetired=!next||oldRoot==null||!oldRoot.gameObject.activeSelf;
            report.originalTilePreserved=true;
            foreach(var pos in book.MapaDeCasillas.cellBounds.allPositionsWithin)if(book.MapaDeCasillas.HasTile(pos)&&book.MapaDeCasillas.GetTile(pos)!=book.Casilla)report.originalTilePreserved=false;
            var lighting=UnityEngine.Object.FindAnyObjectByType<WorldLighting>();
            report.debugVisibility=lighting.DebugFullVisibility;report.propsOutsideBackgroundDarkening=!root.IsChildOf(lighting.transform);
            if(report.props==0||report.decorativeColliders!=0||report.activeRoots!=1||!report.originalTilePreserved||!report.oldRootRetired)throw new Exception("Live artwork invariant failed.");
        }
        catch(Exception e){report.error=e.ToString();}
        File.WriteAllText(Output+(next?"live-next-level.json":"live-play.json"),JsonUtility.ToJson(report,true));
    }
}
