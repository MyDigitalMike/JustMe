using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Auth : MonoBehaviour
{
    protected Firebase.Auth.FirebaseAuth auth;
    protected Firebase.Auth.FirebaseUser user;
    private string displayName;
    private bool signedIn;
    private bool LoginT = false;

    public InputField InputFieldEmail;
    public InputField InputFieldPassword;
    private void Start()
    {
         InitializeFirebase();
    }
    private void Update()
    {
        if (LoginT)
        {
            ActiveSession();
            PerfilUsuario();
            ActiveSession();
            SceneManager.LoadScene("MainMenu");
        }
    }
    void InitializeFirebase()
    {
        auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;
        AuthStateChanged(this, null);
    }

    void AuthStateChanged(object sender, System.EventArgs eventArgs)
    {
        if (auth.CurrentUser != user)
        {
            bool signedIn = user != auth.CurrentUser && auth.CurrentUser != null;
            if (!signedIn && user != null)
            {
                Debug.Log("Signed out " + user.UserId);
            }
            user = auth.CurrentUser;
            if (signedIn)
            {
                Debug.Log("Signed in " + user.UserId);
                displayName = user.DisplayName ?? "";
                //emailAddress = user.Email ?? "";
                //photoUrl = user.PhotoUrl ?? "";             
            }
        }
    }
    public void CreateUserEmail()
    {
        string email = InputFieldEmail.text;
        string password = InputFieldPassword.text ;
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("CreateUserWithEmailAndPasswordAsync was canceled.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("CreateUserWithEmailAndPasswordAsync encountered an error: " + task.Exception);
                return;
            }

            // Firebase user has been created.
            Firebase.Auth.FirebaseUser newUser = task.Result;
            Debug.LogFormat("Firebase user created successfully: {0} ({1})",
                newUser.DisplayName, newUser.UserId);
        });
    }
    public void IniciarSesion()
    {
        string email = InputFieldEmail.text;
        string password = InputFieldPassword.text;
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
            if (task.IsCanceled)
            {
                Debug.LogError("SignInWithEmailAndPasswordAsync was canceled.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("SignInWithEmailAndPasswordAsync encountered an error: " + task.Exception);
                return;
            }

            Firebase.Auth.FirebaseUser newUser = task.Result;
            Debug.LogFormat("User signed in successfully: {0} ({1})",
                newUser.DisplayName, newUser.UserId);
        });
    }
    public void ActiveSession()
    {
        //Firebase.Auth.FirebaseAuth auth;
        //Firebase.Auth.FirebaseUser user;

        // Handle initialization of the necessary firebase modules:
        //void InitializeFirebase()
        //{
        //    Debug.Log("Setting up Firebase Auth");
        //    auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        //    auth.StateChanged += AuthStateChanged;
        //    AuthStateChanged(this, null);
        //}

        // Track state changes of the auth object.
        //void AuthStateChanged(object sender, System.EventArgs eventArgs)
        //{
        //    if (auth.CurrentUser != user)
        //    {
        //        bool signedIn = user != auth.CurrentUser && auth.CurrentUser != null;
        //        if (!signedIn && user != null)
        //        {
        //            Debug.Log("Signed out " + user.UserId);
        //        }
        //        user = auth.CurrentUser;
        //        if (signedIn)
        //        {
        //            Debug.Log("Signed in " + user.UserId);
        //        }
        //    }
        //}
        //void OnDestroy()
        //{
        //    auth.StateChanged -= AuthStateChanged;
        //    auth = null;
        //}
    }
    public void PerfilUsuario()
    {
        Firebase.Auth.FirebaseUser user = auth.CurrentUser;
        if (user != null)
        {
            string name = user.DisplayName;
            string email = user.Email;
            System.Uri photo_url = user.PhotoUrl;
            // The user's Id, unique to the Firebase project.
            // Do NOT use this value to authenticate with your backend server, if you
            // have one; use User.TokenAsync() instead.
            string uid = user.UserId;
        }
    }
    public void OnLogin()
    {
        string email = InputFieldEmail.text;
        string password = InputFieldPassword.text;
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
            if (task.IsCanceled)
            {
                Debug.LogError("SignInWithEmailAndPasswordAsync was canceled.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("SignInWithEmailAndPasswordAsync encountered an error: " + task.Exception);
                return;
            }

            Firebase.Auth.FirebaseUser newUser = task.Result;
            Debug.LogFormat("User signed in successfully: {0} ({1})",
                newUser.DisplayName, newUser.UserId);
            LoginT = true;
        });
    }
}
