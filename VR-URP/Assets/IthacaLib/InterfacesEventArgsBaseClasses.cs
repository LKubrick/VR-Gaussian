/*!------ InterfacesEventArgsBaseClasses  ------------------------------------------------------
* @brief 
*   Core and base classes for all modules.
*
*-----------------------------------------------------------------------------------------------
* @project IthacaLib by French Touch Factory
* @resp Quentin
* @author Quentin de Cagny ( quentindecagny@gmail.com)
* @date created in june 2023, last modified in september 2023
* @details 
*   Contains all interfaces, base classes and eventargs constituting the back bone 
*   structure of IthacaLib. All modules (sources, bridges, targets will derive from one of the
*   models base classes here)
*   Problem: a modular structure needs some pooling of core functionalities
*   Hypothesis: conceptualisation of a core structure for IthacaLib
*   Solution: this script gathers all core base classes, events and interfaces definitions that
*   are needed by the modules.
* @note
* @test Works fine since more than one year now...
*---------------------------------------------------------------------------------------------*/

using System;
using UnityEngine;
using Component = UnityEngine.Component;
using System.Collections.Generic;
using IthacaLib.Common;

#if USINGK4A
using K4AdotNet.Sensor;
#endif

namespace IthacaLib
{
#region SOURCE TARGET MODULES STRUCTURE

    // EVENTARGS

    /// <summary>
    /// Generic eventargs for transmitting a "frame data" from a source to a traget module.
    /// </summary>
    /// <typeparam name="TFrame">Example: Texture2D, K4ADotNet.Image...</typeparam>
    public class DataEventArgs<TFrame> : EventArgs
    {
        public DataEventArgs(TFrame output, FrameInfo frameInfo) { Output = output; FrameInfo = frameInfo; }
        public TFrame Output { get; }
        /// <summary>FrameID is holding a counter of the "frames" delivered by the source of the datas. 
        /// BUT, if it is synchronized (by KinectsSynchronizer for example) some frameIDs can be jumped 
        /// to keep the same frameID between synchronized frames. </summary>
        public FrameInfo FrameInfo { get; }
    }
    public class FrameInfo
    {
        public int cameraIndex;
        public int frameID;
        /// <summary>
        /// in microseconds.</summary>
        public long timestamp;

        public FrameInfo(int cameraIndex, int frameID, long timestamp) 
        { 
            this.cameraIndex = cameraIndex;
            this.frameID = frameID;
            this.timestamp = timestamp;
        }
    }

    // INTERFACES
    public interface IBufferCreator
    {
        public virtual RenderTexture PrepareColorBuffer(VideoResolution res)
        {
            var colorsOut = new RenderTexture(res.width, res.height, 0, RenderTextureFormat.ARGB32)
            {
                graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,
                enableRandomWrite = true,
                useMipMap = false,
                filterMode = FilterMode.Point
            };
            colorsOut.Create();
            return colorsOut;
        }

        public virtual RenderTexture PreparePositionBuffer(VideoResolution res)
        {
            var positionsOut = new RenderTexture(res.width, res.height, 0, RenderTextureFormat.ARGBFloat)
            {
                enableRandomWrite = true,
                useMipMap = false,
                filterMode = FilterMode.Point
            };
            positionsOut.Create();
            return positionsOut;
        }
    }
    public interface IModule
    {
        ///<summary>Event triggered when something has changed in the initialisation parameters of the Source (and the first time the Source is initialized) that 
        ///require some (re)initialisation to be propagated among the chained modules.
        ///WARNING ! if you override this event, you should also override HotInit function or call base.HotInit() somewhere smart to propagate the event as expected.</summary>
        event EventHandler HotInitialized;
        ///<summary>Event triggered by a module which knows it has just finished the last frame. This event propagates itself along the whole pipeline till the end.
        ///WARNING ! if you override this event, you should also override FinishProcess function and/or make all your Invoke by hand (calling base.FinishProcess() won't work) to propagate the event as expected.</summary>
        event EventHandler HasFinished;
    }

    public interface ISourceModule<TFrame> : IModule
    {
        ///<summary>Event triggered when the TFrame data is fresh new (aka a new data is available). Listen this event to receive it (encapsulated in the eventArgs) ASAP.</summary>
        event EventHandler<DataEventArgs<TFrame>> OutputReady;
    }

    public interface IInjectable
    {
        public bool SettingsByContext { get; set; }
        public void Subscribe()
        {
            if(SettingsByContext)
                ContextManager.AddToInjectables(this); 
        }

        public void Unsubscribe()
        {
            if (SettingsByContext)
                ContextManager.RemoveInjectable(this);
        }
        public void GetSettingsByContext();
        public void PropagateToContext();
    }

    // BASE CLASSES

    public abstract class ModuleBase : MonoBehaviour, IInjectable
    {
        /// <summary>Used for initialisation at first frame or "hot" reinit during operation. Just pass this to false and (re)init will occur next time a frame arrives.</summary>
        public bool IsHotInitComplete { get; set; } = false;
        public bool settingsByContext = false;
        public bool SettingsByContext { get => settingsByContext; set => settingsByContext = value; }

        /// <summary>Called by propagation of HasFinished event from the Source module at the end of a pipeline one every modules (i.e when cameras are stopped or if end of video file is reached...). See also OnHasFinishedFromSource function.</summary>
        public abstract void FinishProcess();
        /// <summary>Function for cleaning up memory and other resources used by a module.</summary>
        public virtual void CleanUp() { }
        protected virtual void Reset() 
        {
            FindContextInstance();
            ((IInjectable)this).Subscribe();
        }
        protected virtual void OnDestroy()
        {
            ((IInjectable)this).Unsubscribe();
            IsHotInitComplete = false;
            //FinishProcess();  // All things considered, we don't want to call this outside of the HasFinished event propagation system.
            CleanUp();
        }
        public virtual void GetSettingsByContext()
        {
            settingsByContext = false;
            Debug.LogWarning("No settings can be set from the context for this module yet");

        }

        public virtual void PropagateToContext()
        {
            Debug.LogError("This module cannot set any settings in the context yet");
        }

        public void FindContextInstance()
        {
            if (ContextManager.instance == null)
            {
                try
                {
                    ContextManager.TryFindInstance();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning(e.Message);
                    return;
                }
            }
        }
    }

    ///<summary>Base class for modules that are a source i.e that emit events with some data of type TFrame to all listeners.</summary>
    public abstract class SourceModuleBase<TFrame> : ModuleBase, ISourceModule<TFrame>
    {
        /// <summary>(Re)Initialisation that can occur "hotly" i.e during process. By default this virtual function  
        /// just put the bool IsHotInitComplete to true AND PROPAGATES THE HotInitialized event. Override it for more  
        /// complex hot initialisations.</summary>
        public virtual void HotInit() 
        {
            //Debug.Log("HotInit in SOURCE " + this.GetType().Name + " (" + this.name + ")");
            IsHotInitComplete = true; 
            HotInitialized?.Invoke(this, EventArgs.Empty);
        }

        public virtual event EventHandler HotInitialized;
        public abstract event EventHandler<DataEventArgs<TFrame>> OutputReady;
        public virtual event EventHandler HasFinished;

        public override void FinishProcess() 
        { 
            HasFinished?.Invoke(this, EventArgs.Empty); 
            Debug.Log("FinishProcess in SOURCE " + this.GetType().Name + " (" + this.name + ")");
        }
    }


    ///<summary>Base class for modules that are a target i.e that receives some data of type TFrame from a source.</summary>
    public abstract class TargetModuleBase<TFrame> : ModuleBase, IModule
    {
        /// <summary>Source module from which this module will receive data after having been connected to it's OutputReady event (by calling Connect() ). To set another module as source, it is important to Disconnect() before and Connect() after setting.</summary>
        public Component source;
        public ISourceModule<TFrame> Source
        {
            get { return source ? (ISourceModule<TFrame>)source : null; }
            set { source = (Component)value; }
        }
        // Note: this Component + ISourceModule association with a get/set is only usefull for the possibility to drag&drop a component
        // in the Unity Inspector to specify the source.

        public virtual event EventHandler HotInitialized;
        public virtual event EventHandler HasFinished;

#region Unity framework logic
        // Unity framework functions -
        // Best practice : in order to be "Unity free" ready, put nothing here that
        // could not be done if these Start, OnValidate, Update... calls didn't exist. 
        protected virtual void Start()
        {
            Connect();
        }

        protected virtual void OnValidate()
        {
            //CHECK if Component plugged in is really a ISourceModule<TFrame>...
            if (source != null)
                if (source is not ISourceModule<TFrame>)
                {   //... if not, tries to find a ISourceModule among the Components of source (which is probably a transform at this point)
                    var sources = source.GetComponents<ISourceModule<TFrame>>();
                    
                    if (sources.Length > 0)
                    {
                        source = (Component)sources[^1];
                        Debug.Log("ISourceModule<" + typeof(TFrame) + "> found among the components of the gameObject");
                    }
                    else
                        Debug.LogError(this.GetType().Name + "(in "+ this.name + "): The component you tried to plug in is not a ISourceModule<" + typeof(TFrame) + ">");
                }
            IsHotInitComplete = false;
        }

        protected override void OnDestroy()
        {
            Disconnect();
            base.OnDestroy();
        }
        #endregion

        /// <summary>(Re)Initialisation that can occur "hotly" i.e during process. By default this virtual base function just put the bool IsHotInitComplete to true. Override it for more complex hot initialisations.</summary>
        public virtual void HotInit(DataEventArgs<TFrame> e) 
        {
            //Debug.Log("HotInit in TARGET " + this.GetType().Name + " (" + this.name + ")");
            IsHotInitComplete = true;
            HotInitialized?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>This function just sets a bool to false to ensure the HotInit function will be called next time there is a data coming from the Source. Can be overrided for more complex instructions.</summary>
        public virtual void OnHotInitFromSource(object sender, EventArgs e) 
        {
            //Debug.Log("OnHotInitFromSource in TARGET " + this.GetType().Name + " (" + this.name + ")");
            IsHotInitComplete = false; 
        }

        /// <summary>This function just sets a bool to false to ensure the HotInit function will be called next time there is a data coming from the Source. Can be overrided for more complex instructions.</summary>
        public virtual void OnHasFinishedFromSource(object sender, EventArgs e) { FinishProcess(); }
        public override void FinishProcess() 
        {
            //Debug.Log("FinishProcess in TARGET " + this.GetType().Name + " (" + this.name + ")");
            HasFinished?.Invoke(this, EventArgs.Empty);
        }


        ///<summary>Effectively connects the module by subscribing to the Source(s) events.</summary>
        public virtual void Connect()
        {
            Debug.Log("Connecting " + this.GetType().Name + " (in " + this.name + ") to source: " + Source?.GetType());
            if (Source != null)
            {
                Source.HotInitialized += OnHotInitFromSource;
                Source.OutputReady += ProcessData;
                Source.HasFinished += OnHasFinishedFromSource;
            }
            else
                Debug.LogWarning(this.ToString() + " : trying to Connect to a " + typeof(TFrame) + " Source at null.");
        }

        ///<summary>Effectively DISconnects the module by UNsubscribing to the Source(s) events.</summary>
        public virtual void Disconnect()
        {
            if (Source != null)
            {
                Source.HotInitialized -= OnHotInitFromSource;
                Source.OutputReady -= ProcessData;
                Source.HasFinished -= OnHasFinishedFromSource;
            }
            else
                Debug.LogWarning(this.ToString() + " : trying to DISconnect from a " + typeof(TFrame) + " Source at null.");
        }

        /// <summary>You can put a call to this base implementation at the BEGINNING of your overrided function to hide the HotInit/IsHotInitComplete mechanism.</summary>
        public virtual void ProcessData(object sender, DataEventArgs<TFrame> e)
        {
            if (!IsHotInitComplete) HotInit(e);
        }
    }


    ///<summary>Base class for modules that are a target and a source i.e a bridge.</summary>
    public abstract class BridgeModuleBase<TFrameIn, TFrameOut> : TargetModuleBase<TFrameIn>, ISourceModule<TFrameOut>
    {
        public abstract event EventHandler<DataEventArgs<TFrameOut>> OutputReady;
    }
#endregion

#region METAEVENTS
    ///<summary>Generic events at disposition for various purposes. A module (or anything) can emit events on this "shared channel of events" and other modules can listen to it to catch the events they're interested in.</summary>
    public interface IMetaEventEmiter
    {
        event EventHandler<MetaEventArgs> OnMetaEvent;
    }

    public interface ISynchronizer
    {
        public List<EventHandler<SynchronizationEventArgs>> SynchronizationEvents { get; set; }
    }

    public enum MetaEventType
    {
        Started,        // Triggered when a module starts delivering frames
        Stopped,        // Triggered when a module stops delivering frames, and/or has reached the end of the data he can deliver
        Paused,         // Triggered when a module pauses for any reason
        Looping,        // Triggered when a looping source has reached the end and that the next frame will be the first one again.
        PipelineStarted,// Triggered when the first module of the current pipeline starts. Code can hold a number identifying the pipeline in case of several pipelines running.
        PipelineFinished,// Triggered when the current pipeline finished all its processes. Code can hold a number identifying the pipeline in case of several pipelines running.
        FrameDropped,   // Triggered when a module is able to detect that a frame has been lost before its process
        FrameDropping,  // Triggered when a module is able to detect that it will not deliver a frame
        LastFrameID,    // Code = the frameID. Triggered when some module can give the information of the frameID of the last frame of the current pipeline.
        Error,          // Code = some Error code. Triggered when an error of any type occurs
        Custom          // Used for any unplanned event, with usage of Code and Message to precise the content.

        //OTHER POSSIBILITIES
        // HotInitialized : could maybe replace, or complete, the hotInitialized mecanism
        // EndReached, 
        // LoopEndReached,
    }

    ///<summary>Generic events used for various purposes. A module (or anything) can emit events on this shared channel of events and other modules can listen to it to catch the events they're interested in.</summary>
    public class MetaEventArgs : EventArgs
    {
        public MetaEventArgs(MetaEventType se, int code, string message) { StreamEvent = se; Code = code; Message = message; }
        public MetaEventType StreamEvent { get; }
        public int Code { get; }
        public string Message { get; }
    }

    public class SynchronizationEventArgs : EventArgs
    {
        public SynchronizationEventArgs(bool f, int n) { forward = f; nbFrames = n; }
        public bool forward { get; }
        public int nbFrames { get; }
    }
#endregion

#region SPECIAL PURPOSE EVENTS
//(Used by KinectsManager)
#if USINGK4A
    public class KinectConfigChangedArgs : EventArgs
    {
        public KinectConfig kinectConfig { get; }

        public KinectConfigChangedArgs(KinectConfig config)
        {
            kinectConfig = config;
        }
    }
    public class IMUEventArgs : EventArgs
    {
        public IMUEventArgs(ImuSample sample) { IMUSample = sample; }
        public ImuSample IMUSample;
    }
#endif
#endregion
}
