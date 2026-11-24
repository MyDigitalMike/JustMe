using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.Cinemachine;
[InitializeOnLoad]
public static class Phase4BaselineProbe
{
    [Serializable] class Result {public int initialLevel,finalLevel;public bool exitStayedLocked;public string error;}
    static Result pending;static int frame;
    static Phase4BaselineProbe(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        if(pending!=null && Time.frameCount>frame+90)
        {
            var book=UnityEngine.Object.FindAnyObjectByType<Libro>();pending.finalLevel=book!=null?book.Nivel:-1;
            pending.exitStayedLocked=pending.initialLevel==pending.finalLevel;
            File.WriteAllText("Backups/Phase4/locked-exit-before.json",JsonUtility.ToJson(pending,true));pending=null;
        }
        const string request="Temp/Phase4/baseline.request";if(!File.Exists(request))return;
        try
        {
            File.Delete(request);var book=UnityEngine.Object.FindAnyObjectByType<Libro>();
            if(book==null || book.LastGeneratedWorld==null)throw new Exception("Gameplay not ready");
            pending=new Result{initialLevel=book.Nivel};frame=Time.frameCount;
            Player player=book.Jugador.GetComponent<Player>();var body=player.GetComponent<Rigidbody2D>();
            Vector3 destination=book.Elemento.transform.position,delta=destination-player.transform.position;
            player.transform.position=destination;body.position=destination;body.linearVelocity=Vector2.zero;player.NotifyWarp();
            CinemachineCore.OnTargetObjectWarped(player.transform,delta);if(Camera.main!=null)Camera.main.transform.position+=delta;
        }
        catch(Exception e){File.WriteAllText("Backups/Phase4/locked-exit-before.json",JsonUtility.ToJson(new Result{error=e.ToString()},true));pending=null;}
    }
}
