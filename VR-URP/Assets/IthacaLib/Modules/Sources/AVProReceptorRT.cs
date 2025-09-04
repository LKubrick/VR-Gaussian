/*#############################################################################################
#  ----   AVProReceptorRT   ----
# source of: --> RenderTexture (B8G8R8A8_UNORM)
#
# --------------------------------------------------------------------------------------------
#           $$$$$ IMPORTANT $$$$$$
# To use AVPro, you must : 
# - install AVPro in your project : https://www.renderheads.com/content/docs/AVProVideo/articles/install.html
# Core Edition from the Unity Asset store should be fine. You can try trial version too (but will it work???)
# - define the symbol USINGAVPRO (go to Project settings > Other Settings > Script compilation
# and add a Scrpting Define Symbol in the list with name USINGAVPRO.
# - add a MediaPlayer component on a GameObject somewhere in the scene.

# AND 
#
#then, to use LightShaft (for youtube live streaming capability) :
# - install LigthShaft's Youtube Video Player + Youtube API :
# https://assetstore.unity.com/packages/tools/video/youtube-video-player-youtube-api-29704
# - define the symbol USINGLIGHTSHAFT (go to Project settings > Other Settings > Script compilation
# and add a Scrpting Define Symbol in the list with name USINGLIGHTSHAFT.
# _ uncomment AVPro parts in the lightShaft's script YoutubePlayerLivestream
# - add a YoutubePlayerLivestream component on your AVPro's MediaPlayer
#
# (note those two assets are not free and must be purchased)
#
# --------------------------------------------------------------------------------------------
# Project: IthacaEmetteurRecepteur by FrenchTouchFactory
# Resp. : Quentin
# Author(s): 
# Quentin de Cagny ( quentindecagny@gmail.com), inspired from AVPro Video/Display uGUI
# Date : created in june 2023, last modified in september 2023
# Description: Module for handling an AVPro receptor. As a source of RenderTexture it sends 
#   streams of events with Unity RenderTexture each time a new frame arrives from AVPro.
# Parameters : -
# Note : Compatible with AVpro 2 and 3
# Tests : -
#############################################################################################*/


using System;
using UnityEngine;
using System.Linq;


//using UnityEngine.Experimental.Rendering;

#if USINGAVPRO
using RenderHeads.Media.AVProVideo;

namespace IthacaLib
{
    /// <summary>
    /// Module for handling an AVPro receptor. As a source of RenderTexture it sends streams of events with Unity RenderTexture each time a new frame arrives from AVPro.
    /// </summary> 
    [ExecuteInEditMode]
    public class AVProReceptorRT : SourceModuleBase<RenderTexture>
    {
        public override event EventHandler<DataEventArgs<RenderTexture>> OutputReady;
        public override event EventHandler HotInitialized;


        [Header("This module is a RenderTexture source")]
        [Tooltip("There must be a AVPro MediaPlayer in your project. It should find it automatically on Play, otherwise please put it here.")]
        [SerializeField] MediaPlayer _mediaPlayer;

        // To use Youtube Live Streams, install LightShaft :
#if USINGLIGHTSHAFT
        [Tooltip("URL of a youtube live channel Ex: \"https://www.youtube.com/watch?v=wUz1Vb7I2DU\" ")]
        public string youtubeLiveURL = "https://www.youtube.com/watch?v=LpoyHAwT7mE"; // Nautilus Live | Channel 2 Stream for test
#endif
        [Tooltip("If AVPro doesn't provide a Texture in this target resolution, a resampling will occur.")]
        [SerializeField]
        private Vector2Int targetResolution = new Vector2Int(1280, 720);

        public Vector2Int TargetResolution
        {
            get { return targetResolution; }
            set { targetResolution = value; hotInitialized = false; }
        }

        [Tooltip("Pass this to true if you wish to force the resolution of the renderTextures obtained from AVPro to the values specified in targetResolution. Otherwise the resolution of the RT obtained from the media player will be used")]
        public bool ForceTargetResolution = false;

        [Tooltip("Vertically flips the image. DO NOT CHANGE unless you know what you're doing : Exposed here for clarity but this setting is set depending on the system (Windows:true, Android:false)")]
        [SerializeField]
#if UNITY_ANDROID || UNITY_WEBGL
        private bool flipRenderTexture = false;    //I inversed the flip after the new auto layouts calculator (UniZOrder)
#elif UNITY_STANDALONE_WIN
        private bool flipRenderTexture = true;     //I inversed the flip after the new auto layouts calculator (UniZOrder)
#endif
        // Be aware: the default setting of flipRenderTexture must be changed also in HotInit. This affectation is just here for correct display in Inspector.

        private Vector2 scaleRT = new Vector2(1, 1);
        private Vector2 offsetRT = new Vector2(0, 0);



        private FrameInfo currentFrameInfo;
        private RenderTexture currentRT;
        private RenderTexture currentRT1;
        private RenderTexture currentRT2;
        private bool useCurrentRT2 = false;
        bool hotInitialized = false;
        private int lastFrame = 0; // Number of the last Texture frame, for launching new Texture2DReady event

        //DEBUG STUFFS
        [Tooltip("Resolution of the Texture given by AVPro's MediaPlayer at each frame.")]
        public Vector2Int debugTextureRes = Vector2Int.zero;


        #region Unity framework logic
        void OnEnable()
        {
            if (_mediaPlayer == null) _mediaPlayer = GameObject.FindAnyObjectByType<MediaPlayer>();
#if USINGLIGHTSHAFT
            if (youtubeLiveURL != null) _mediaPlayer.GetComponent<YoutubePlayerLivestream>()._livestreamUrl = youtubeLiveURL;
#endif
        }

        void OnValidate()
        {
            if (_mediaPlayer == null) _mediaPlayer = GameObject.FindAnyObjectByType<MediaPlayer>();
#if USINGLIGHTSHAFT
            if (youtubeLiveURL != null) _mediaPlayer.GetComponent<YoutubePlayerLivestream>()._livestreamUrl = youtubeLiveURL;
#endif
            hotInitialized = false;
        }

        public void Update()
        {
            if (enabled)
            {

                if (_mediaPlayer != null && _mediaPlayer.TextureProducer != null)
                {
                    if (lastFrame != _mediaPlayer.TextureProducer.GetTextureFrameCount())
                    {
                        if (!hotInitialized) HotInit(_mediaPlayer.TextureProducer.GetTexture()); //ERREUR RARE : il arrive qu'on n'ait pas de Texture ici (du coup le debug.log dans l'initialise ne marche pas)
                        if (!hotInitialized) return;
                        currentFrameInfo = new(0, _mediaPlayer.TextureProducer.GetTextureFrameCount(), _mediaPlayer.TextureProducer.GetTextureTimeStamp());

                        if (OutputReady != null)
                        {   //If no one listen for a RT, we do no blit and no invoke
   
                            Graphics.Blit(_mediaPlayer.TextureProducer.GetTexture(), currentRT, scaleRT, offsetRT);  //... so we do a blit for the conversion into a RenderTexture and for 
                            OutputReady?.Invoke(this, new DataEventArgs<RenderTexture>(currentRT, currentFrameInfo));  // its "magic" resizing property (if needed)
                        }

                        lastFrame = _mediaPlayer.TextureProducer.GetTextureFrameCount();

                        //DEBUGS
                        if (_mediaPlayer.TextureProducer.GetTexture() != null)
                            debugTextureRes = new Vector2Int(_mediaPlayer.TextureProducer.GetTexture().width, _mediaPlayer.TextureProducer.GetTexture().height);
                    }
                }
            }
        }
        protected override void OnDestroy()
        {
#if UNITY_EDITOR
            foreach (var targets in UnityEditor.EditorApplication.update.GetInvocationList().Where(d => d.Target == this))
            {
                UnityEditor.EditorApplication.update -= Update;
            }
#endif
            base.OnDestroy();
            currentRT = null;
            RenderTexture.ReleaseTemporary(currentRT1);
            RenderTexture.ReleaseTemporary(currentRT2);
        }
#endregion

        ///<summary>Creates or recreates the RenderTexture, sets some parameters.</summary>
        void HotInit(Texture AVProTexture)
        {
            if (AVProTexture == null) return;

            if (SystemInfo.deviceType == DeviceType.Desktop) flipRenderTexture = true;
            else flipRenderTexture = false;
            // Be aware: the default setting of flipRenderTexture must be changed also at line 80 (or close)

            scaleRT = flipRenderTexture ? new Vector2(1, -1) : new Vector2(1, 1);
            offsetRT = flipRenderTexture ? new Vector2(0, 1) : new Vector2(0, 0);
            if(!ForceTargetResolution)
            {
                targetResolution.Set(AVProTexture.width, AVProTexture.height);
            }
            // currentRT = new RenderTexture(targetResolution.x, targetResolution.y, 0, AVProTexture.graphicsFormat);
            // //currentRT.memorylessMode = RenderTextureMemoryless.Color; // Very very touchy stuuf, need to be investigated... could speed up things on mobiles, maybe...

            var rtReadWriteFormat = RenderTextureReadWrite.Linear;
            // WIP: this is a double RT system designed to get rid of error "Setting width of already created render texture is not supported!"
            // but it just corrects the error "CopyTexture called with mismatching texture sizes". Well it's working, but it seems dirty.
            // The important stuff here is to understand how to get rid of a RenderTexture in memory in a clean way.
            if (!useCurrentRT2)
            {
                if (currentRT2 != null) RenderTexture.ReleaseTemporary(currentRT2);
                currentRT1 = RenderTexture.GetTemporary(targetResolution.x, targetResolution.y, 0, RenderTextureFormat.BGRA32, rtReadWriteFormat);
                currentRT = currentRT1;
            }
            else
            {
                if (currentRT1 != null) RenderTexture.ReleaseTemporary(currentRT1);
                currentRT2 = RenderTexture.GetTemporary(targetResolution.x, targetResolution.y, 0, RenderTextureFormat.BGRA32, rtReadWriteFormat);
                currentRT = currentRT2;
            }
            Debug.Log("AVProReceptorRT: Initializing currentRT with graphicsFormat " + currentRT.graphicsFormat);

            currentRT.filterMode = FilterMode.Point;

            HotInitialized?.Invoke(this, EventArgs.Empty);
            if (_mediaPlayer != null)
            {
                //Q:: our script will listen to all of the events launched by the mediaPlayer and pass it to OnMediaPlayerEvent for treatment
                _mediaPlayer.Events.AddListener(OnMediaPlayerEvent);
            }
            _mediaPlayer.UseResampler = false;
            hotInitialized = true;
        }


        ///<summary>Returns true when app is playing and a mediaPlayer and TextureProducer exists and the GetTexture() returns something.</summary>
		public bool HasValidTexture()
        {
            return (Application.isPlaying && _mediaPlayer != null && _mediaPlayer.TextureProducer != null && _mediaPlayer.TextureProducer.GetTexture() != null);
        }

        // Callback function to handle AVProVideo events
        private void OnMediaPlayerEvent(MediaPlayer mp, MediaPlayerEvent.EventType et, ErrorCode errorCode)
        {
            switch (et)
            {
                case MediaPlayerEvent.EventType.FirstFrameReady:
                    Debug.Log("AVPro event: FirstFrameReady - TextureFrameCount = " + mp?.TextureProducer?.GetTextureFrameCount());
                    //Q:: We keep this not usefull lines just for future implementation of events (from AVPro) handling.
                    // if (_isUserMaterial && null != GetRequiredShader())
                    //     Debug.LogWarning("[AVProVideo] Custom material is being used but the video requires our internal shader for correct rendering.  Consider removing custom shader or modifying it for AVPro Video support.", this);
                    // LateUpdate();
                    break;
                case MediaPlayerEvent.EventType.ResolutionChanged:
                    Debug.Log("AVPro event: Resolution changed for " + mp?.TextureProducer?.GetTexture()?.width);
                    //LateUpdate();
                    break;
                case MediaPlayerEvent.EventType.PropertiesChanged:
                    Debug.Log("AVPro event: PropertiesChanged ");
                    //LateUpdate();
                    break;
                case MediaPlayerEvent.EventType.Closing:
                    Debug.Log("AVPro event: Closing ");
                    //LateUpdate();
                    break;
            }

            // Q:: We keep this call to LateUpdate to remember that AVPro does a lot of stuff in LiveUpdate mode, but we'll probably won't need it.
            // AVPro: We do a LateUpdate() to allow for any changes in the texture that may have happened in Update()
            // LateUpdate();
        }
        /// <summary>
		/// Returns the current (Unity) Texture produced by AVPro if needed outside the event system.
		/// </summary>
		public Texture mainTexture
        {
            get
            {
                Texture result = Texture2D.whiteTexture;
                if (HasValidTexture())
                    result = _mediaPlayer.TextureProducer.GetTexture();

                else
                    result = null;

                return result;
            }
        }
    }
}
#else

namespace IthacaLib
{
    /// <summary>
    /// Module for handling an AVPro receptor. But AVPRO is unavailable on this environnement, please install it and define #USINGAVPRO symbol.
    /// </summary>
    public class AVProReceptorRT : MonoBehaviour
    {

        [TextArea(2, 2)]
        [SerializeField]
        string ________moduleInfos = "AVPro is NOT AVAILABLE on this environnement,\r\n" +
            " please install it and define #USINGAVPRO symbol. ";
    }
}
#endif
