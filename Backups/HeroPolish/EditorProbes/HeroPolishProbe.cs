using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class HeroPolishProbe
{
    const string Output="Backups/HeroPolish/";
    static HeroPolishProbe(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string path="Temp/HeroPolish/request";if(EditorApplication.isCompiling||!File.Exists(path))return;
        string action;try{action=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try{if(action=="audit")Audit();else if(action=="paths")Paths();else if(action=="apply")Apply();else if(action=="verify")Verify();}
        catch(Exception e){File.WriteAllText(Output+"error-"+action+".txt",e.ToString());}
    }
    static void Audit()
    {
        var p=UnityEngine.Object.FindAnyObjectByType<Player>();if(p==null||!EditorApplication.isPlaying)throw new Exception("Needs live gameplay");
        var visual=p.GetComponent<HeroVisualAlignment>().Visual;var rb=p.GetComponent<Rigidbody2D>();var lines=new List<string>();
        lines.Add("Grounded="+p.Grounded+" state="+p.State+" root="+rb.position+" soleY="+(rb.position.y+p.SoleOffset));
        lines.Add("sprite="+visual.sprite.name+" rect="+visual.sprite.rect+" pivot="+visual.sprite.pivot+" bounds="+visual.bounds+" sortLayer="+visual.sortingLayerID+" order="+visual.sortingOrder);
        lines.Add("material="+AssetDatabase.GetAssetPath(visual.sharedMaterial)+" shader="+visual.sharedMaterial.shader.name);
        var shader=visual.sharedMaterial.shader;for(int i=0;i<ShaderUtil.GetPropertyCount(shader);i++)lines.Add("shaderProperty="+ShaderUtil.GetPropertyName(shader,i));
        var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(visual.sprite.texture)));var rect=visual.sprite.rect;int minY=tex.height;
        for(int y=(int)rect.y;y<rect.yMax;y++)for(int x=(int)rect.x;x<rect.xMax;x++)if(tex.GetPixel(x,y).a>.5f)minY=Math.Min(y,minY);
        float localY=(minY-rect.y-visual.sprite.pivot.y)/visual.sprite.pixelsPerUnit;lines.Add("opaqueFootY="+visual.transform.TransformPoint(new Vector3(0,localY)).y+" rectFootY="+visual.bounds.min.y+" opaqueInset="+((minY-rect.y)/visual.sprite.pixelsPerUnit));UnityEngine.Object.DestroyImmediate(tex);
        var contacts=new ContactPoint2D[32];int count=rb.GetContacts(contacts);for(int i=0;i<count;i++)lines.Add("contact="+contacts[i].point+" normal="+contacts[i].normal);
        foreach(var tilemap in UnityEngine.Object.FindObjectsByType<Tilemap>())lines.Add("tilemap="+tilemap.name+" anchor="+tilemap.tileAnchor+" sort="+tilemap.GetComponent<TilemapRenderer>().sortingOrder);
        var rope=Resources.LoadAll<Sprite>("HeroTraversal/Props").First(s=>s.name=="Rope");tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(rope.texture)));rect=rope.rect;
        for(int y=0;y<4;y++)lines.Add("rope edge "+y+" alphaBottom="+tex.GetPixel((int)rect.center.x,(int)rect.y+y).a+" alphaTop="+tex.GetPixel((int)rect.center.x,(int)rect.yMax-1-y).a);
        UnityEngine.Object.DestroyImmediate(tex);File.WriteAllLines(Output+"audit.txt",lines);
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Paths()
    {
        var lines=new List<string>();
        foreach(var c in UnityEngine.Object.FindObjectsByType<CompositeCollider2D>())
        {
            lines.Add(c.name+" paths="+c.pathCount+" offset="+c.offset+" geometry="+c.geometryType+" bounds="+c.bounds+" transform="+c.transform.position+" scale="+c.transform.lossyScale+" rb="+c.attachedRigidbody.transform.position);
            int candidates=0,sideOk=0,topOk=0,standingOk=0;var physics=c.gameObject.scene.GetPhysicsScene2D();var p=UnityEngine.Object.FindAnyObjectByType<Player>();var filter=new ContactFilter2D{useTriggers=false};filter.SetLayerMask(p.collisionLayer);var hits=new RaycastHit2D[8];
            for(int path=0;path<c.pathCount;path++)
            {
                var points=new Vector2[c.GetPathPointCount(path)];c.GetPath(path,points);
                for(int i=0;i<points.Length;i++)
                {
                    Vector2 corner=c.transform.TransformPoint(points[i]);Vector2 a=(Vector2)c.transform.TransformPoint(points[(i+points.Length-1)%points.Length])-corner,b=(Vector2)c.transform.TransformPoint(points[(i+1)%points.Length])-corner;
                    if(lines.Count<30)lines.Add("p="+points[i].ToString("F5")+" world="+corner.ToString("F5")+" a="+a.ToString("F5")+" b="+b.ToString("F5"));
                    Vector2 horizontal,vertical;if(Mathf.Abs(a.y)<.002f&&Mathf.Abs(b.x)<.002f){horizontal=a;vertical=b;}else if(Mathf.Abs(b.y)<.002f&&Mathf.Abs(a.x)<.002f){horizontal=b;vertical=a;}else continue;
                    if(vertical.y>=-.04f||Mathf.Abs(horizontal.x)<.04f)continue;candidates++;int inward=horizontal.x>0?1:-1;
                    int n=physics.Raycast(corner+new Vector2(inward*.025f,.05f),Vector2.down,.1f,filter,hits);if(n==0||hits[0].collider!=c||hits[0].normal.y<.9f)continue;topOk++;float top=hits[0].point.y;
                    n=physics.Raycast(corner+new Vector2(-inward*.05f,-.025f),Vector2.right*inward,.1f,filter,hits);if(n==0||hits[0].collider!=c||Mathf.Abs(hits[0].normal.x)<.8f)continue;sideOk++;
                    if(Application.isPlaying&&p.CanStandAt(new Vector2(hits[0].point.x+inward*(p.GetComponent<BoxCollider2D>().bounds.extents.x+.06f),top-p.SoleOffset+.025f),true))standingOk++;
                }
            }
            lines.Add("candidates="+candidates+" top="+topOk+" side="+sideOk+" stand="+standingOk);
        }
        File.WriteAllLines(Output+"paths.txt",lines);
    }
    static ISpriteEditorDataProvider Provider(TextureImporter importer)
    {var f=new SpriteDataProviderFactories();f.Init();var p=f.GetSpriteEditorDataProviderFromObject(importer);p.InitSpriteEditorDataProvider();return p;}
    static void Apply()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Stop Play before import");
        var shader=Shader.Find("JustMe/Artwork Lit");Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Artwork shader failed");
        var registration=new List<string>();
        foreach(string path in new[]{"Assets/Art/Character/AnimeHero/Actions.png","Assets/Art/Character/AnimeHero/Motion.png"})
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);var provider=Provider(importer);var rects=provider.GetSpriteRects();
            var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(path));var pixels=tex.GetPixels32();
            foreach(var sprite in rects)
            {
                Rect old=sprite.rect;int yMin=(int)old.yMax;
                for(int y=(int)old.y;y<old.yMax;y++)for(int x=(int)old.x;x<old.xMax;x++)if(pixels[x+y*tex.width].a>=128)yMin=Math.Min(yMin,y);
                Require(yMin<old.yMax,"No painted sole in "+sprite.name);
                // Crop only empty/very faint padding; keep every existing sprite ID and world pivot.
                Vector2 anchor=old.position+Vector2.Scale(sprite.pivot,old.size);
                sprite.rect=new Rect(old.x,yMin,old.width,old.yMax-yMin);
                sprite.pivot=new Vector2((anchor.x-sprite.rect.x)/sprite.rect.width,(anchor.y-sprite.rect.y)/sprite.rect.height);
                registration.Add(sprite.name+" trimPixels="+(yMin-old.y)+" id="+sprite.spriteID);
            }
            provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            var after=Provider(importer).GetSpriteRects().ToDictionary(r=>r.name,r=>r.spriteID);
            foreach(var sprite in rects)Require(after[sprite.name]==sprite.spriteID,"Sprite ID changed");
            UnityEngine.Object.DestroyImmediate(tex);
        }
        string propsPath="Assets/Resources/HeroTraversal/Props.png";var propsImporter=(TextureImporter)AssetImporter.GetAtPath(propsPath);var propsProvider=Provider(propsImporter);var props=propsProvider.GetSpriteRects();
        var png=new Texture2D(2,2);png.LoadImage(File.ReadAllBytes(propsPath));var colors=png.GetPixels32();
        foreach(var sprite in props)
        {
            if(sprite.name=="Rope")
            {
                // Repeat an interior piece, not the transparent feathered ends of the concept sprite.
                Rect old=sprite.rect;int y=(int)old.y+(int)old.height/4,h=(int)old.height/2;
                if(old.height>400)sprite.rect=new Rect(old.x,y,old.width,h);
                else{y=(int)old.y;h=(int)old.height;}
                sprite.pivot=Vector2.one*.5f;
                Require(png.GetPixel((int)sprite.rect.center.x,y).a>.95f&&png.GetPixel((int)sprite.rect.center.x,y+h-1).a>.95f,"Rope repeat edges are not opaque");
            }
            else if(sprite.name=="Cornice"||sprite.name=="CorniceAlt")
            {
                var tops=new List<int>();Rect rect=sprite.rect;
                for(int x=(int)(rect.x+rect.width*.2f);x<(int)(rect.x+rect.width*.8f);x++)
                    for(int y=(int)rect.yMax-1;y>=rect.y;y--)if(colors[x+y*png.width].a>=128){tops.Add(y);break;}
                tops.Sort();Require(tops.Count>0,"Missing cornice top");float floor=tops[tops.Count/4];
                sprite.pivot=new Vector2(0,(floor-rect.y)/rect.height);
            }
        }
        propsProvider.SetSpriteRects(props);propsProvider.Apply();propsImporter.SaveAndReimport();UnityEngine.Object.DestroyImmediate(png);
        const string materialPath="Assets/Resources/HeroTraversal/Artwork.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}else material.shader=shader;
        material.SetFloat("_AlphaCutoff",.05f);EditorUtility.SetDirty(material);
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Character/Player.prefab");
        try{prefab.GetComponent<SpriteRenderer>().sharedMaterial=material;PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/Character/Player.prefab");}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        AssetDatabase.SaveAssets();File.WriteAllLines(Output+"registration.txt",registration);File.WriteAllText(Output+"applied.txt","Preserved sprite IDs; recropped painted soles; interior rope repeat; corner pivots; lit alpha cleanup material.");
    }
    [Serializable] class Report
    {
        public string error; public int sprites, ledges, ropes; public bool alphaNoiseRejected, opaqueSoles, ropeEdgesOpaque, cornersAligned, cornersSupported, drawOrder, noNewColliders;
        public float footToContact; public List<string> details=new List<string>();
    }
    static void Verify()
    {
        var r=new Report{alphaNoiseRejected=true,opaqueSoles=true,cornersAligned=true,cornersSupported=true,drawOrder=true,noNewColliders=true};
        try
        {
            var p=UnityEngine.Object.FindAnyObjectByType<Player>();Require(p!=null&&EditorApplication.isPlaying,"Needs live gameplay");var visual=p.GetComponent<HeroVisualAlignment>().Visual;
            var material=Resources.Load<Material>("HeroTraversal/Artwork");Require(material!=null&&!ShaderUtil.ShaderHasError(material.shader),"Invalid artwork material");float cutoff=material.GetFloat("_AlphaCutoff");
            Require(visual.sharedMaterial==material,"Hero material not configured");
            foreach(string path in new[]{"Assets/Art/Character/AnimeHero/Actions.png","Assets/Art/Character/AnimeHero/Motion.png"})
            {
                var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(path));var pixels=tex.GetPixels32();int rows=path.Contains("Actions")?6:4,w=tex.width/6,h=tex.height/rows;
                for(int row=0;row<rows;row++)for(int col=0;col<6;col++)for(int y=0;y<h;y++)for(int x=0;x<12;x++)r.alphaNoiseRejected&=pixels[col*w+x+(row*h+y)*tex.width].a/255f<cutoff;
                foreach(var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                {Rect rect=sprite.rect;bool painted=false;for(int x=(int)rect.x;x<rect.xMax;x++)painted|=pixels[x+(int)rect.y*tex.width].a>=128;r.opaqueSoles&=painted;r.sprites++;}
                UnityEngine.Object.DestroyImmediate(tex);
            }
            var rope=Resources.LoadAll<Sprite>("HeroTraversal/Props").First(s=>s.name=="Rope");var png=new Texture2D(2,2);png.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(rope)));
            r.ropeEdgesOpaque=png.GetPixel((int)rope.rect.center.x,(int)rope.rect.y).a>.95f&&png.GetPixel((int)rope.rect.center.x,(int)rope.rect.yMax-1).a>.95f;UnityEngine.Object.DestroyImmediate(png);
            var map=UnityEngine.Object.FindObjectsByType<Tilemap>().First(t=>t.GetComponent<TraversalEnvironment>()!=null);var env=map.GetComponent<TraversalEnvironment>();var terrain=map.GetComponent<TilemapRenderer>();var composite=map.GetComponent<CompositeCollider2D>();
            var stones=env.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name=="Weathered stone edge").ToArray();r.ledges=stones.Length;r.ropes=env.GetComponentsInChildren<SpriteRenderer>().Count(s=>s.name=="Braided climbing rope");
            Require(r.ledges==env.ledgeDecorations.Length&&r.ledges>0,"No verified corner artwork");Require(r.ropes==env.climbableAreas.Length,"Rope region mismatch");
            r.drawOrder=visual.sortingLayerID==terrain.sortingLayerID&&visual.sortingOrder>terrain.sortingOrder+2;
            var physics=map.gameObject.scene.GetPhysicsScene2D();var filter=new ContactFilter2D{useTriggers=false};filter.SetLayerMask(p.collisionLayer);var hits=new RaycastHit2D[8];
            for(int i=0;i<stones.Length;i++)
            {
                var ledge=env.ledgeDecorations[i];var stone=stones[i];r.cornersAligned&=Vector3.Distance(stone.transform.position,ledge.Corner)<.001f&&Mathf.Sign(stone.transform.lossyScale.x)==ledge.Inward;
                r.drawOrder&=stone.sortingLayerID==terrain.sortingLayerID&&stone.sortingOrder==terrain.sortingOrder+2&&visual.sortingOrder>stone.sortingOrder;
                r.noNewColliders&=stone.GetComponent<Collider2D>()==null;
                int n=physics.Raycast((Vector2)ledge.Corner+new Vector2(ledge.Inward*.025f,.05f),Vector2.down,.1f,filter,hits);r.cornersSupported&=n>0&&hits[0].collider==composite&&hits[0].normal.y>.9f&&Mathf.Abs(hits[0].point.y-ledge.Corner.y)<.002f;
                n=physics.Raycast((Vector2)ledge.Corner+new Vector2(-ledge.Inward*.05f,-.025f),Vector2.right*ledge.Inward,.1f,filter,hits);r.cornersSupported&=n>0&&hits[0].collider==composite&&Mathf.Abs(hits[0].normal.x)>.8f&&Mathf.Abs(hits[0].point.x-ledge.Corner.x)<.002f;
            }
            foreach(var renderer in env.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name=="Braided climbing rope"||s.name=="Weathered rope anchor"))r.noNewColliders&=renderer.GetComponent<Collider2D>()==null;
            var contacts=new ContactPoint2D[32];int count=p.GetComponent<Rigidbody2D>().GetContacts(contacts);Require(p.Grounded,"Wait for grounded player");float min=float.MaxValue;
            for(int i=0;i<count;i++)if(contacts[i].normal.y>=p.minimumGroundNormalY)min=Mathf.Min(min,Mathf.Abs(visual.bounds.min.y-contacts[i].point.y));r.footToContact=min;
            Require(min<.015f,"Visible sole does not meet contact");
            Require(r.sprites==60&&r.alphaNoiseRejected&&r.opaqueSoles&&r.ropeEdgesOpaque&&r.cornersAligned&&r.cornersSupported&&r.drawOrder&&r.noNewColliders,"Visual polish check failed");
            r.details.Add("Correct terrain-relative draw order and physical convex top corners, both inward directions.");
        }
        catch(Exception e){r.error=e.ToString();}File.WriteAllText(Output+"verification.json",JsonUtility.ToJson(r,true));
    }
}
