﻿using ArucoUnity.Cameras;
using ArucoUnity.Controllers;
using ArucoUnity.Cameras.Undistortions;
using ArucoUnity.Cameras.Displays;
using ArucoUnity.Plugin;
using ArucoUnity.Utilities;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ArucoUnity
{
  /// \addtogroup aruco_unity_package
  /// \{

  namespace Objects.Trackers
  {
    /// <summary>
    /// Detects <see cref="ArucoObject"/>, displays detections and applies the estimated transforms to gameObjects associated to the ArUco objects.
    /// 
    /// See the OpenCV documentation for more information about the marker detection: http://docs.opencv.org/3.2.0/d5/dae/tutorial_aruco_detection.html
    /// </summary>
    public class ArucoObjectsTracker : ArucoObjectsController, IArucoObjectsTracker
    {
      // Editor fields

      [SerializeField]
      [Tooltip("The camera system to use.")]
      private ArucoCamera arucoCamera;

      [SerializeField]
      [Tooltip("The undistortion process associated with the ArucoCamera.")]
      private ArucoCameraUndistortion arucoCameraUndistortion;

      [SerializeField]
      [Tooltip("The optional camera display associated with the ArucoCamera.")]
      private ArucoCameraGenericDisplay arucoCameraDisplay;

      [SerializeField]
      [Tooltip("Apply refine strategy to detect more markers using the boards in the Aruco Object list.")]
      private bool refineDetectedMarkers = true;

      [SerializeField]
      [Tooltip("Display the detected markers in the CameraImageTexture.")]
      private bool drawDetectedMarkers = true;

      [SerializeField]
      [Tooltip("Display the rejected markers candidates.")]
      private bool drawRejectedCandidates = false;

      [SerializeField]
      [Tooltip("Display the axis of the detected boards and diamonds.")]
      private bool drawAxes = true;

      [SerializeField]
      [Tooltip("Display the markers of the detected ChArUco boards.")]
      private bool drawDetectedCharucoMarkers = true;

      [SerializeField]
      [Tooltip("Display the detected diamonds.")]
      private bool drawDetectedDiamonds = true;

      // ArucoCameraController properties

      public override IArucoCamera ArucoCamera { get { return arucoCamera; } }

      // IArucoObjectsTracker properties

      public IArucoCameraUndistortion ArucoCameraUndistortion { get { return arucoCameraUndistortion; } }
      public IArucoCameraDisplay ArucoCameraDisplay { get { return arucoCameraDisplay; } }
      public bool RefineDetectedMarkers { get { return refineDetectedMarkers; } set { refineDetectedMarkers = value; } }
      public bool DrawDetectedMarkers { get { return drawDetectedMarkers; } set { drawDetectedMarkers = value; } }
      public bool DrawRejectedCandidates { get { return drawRejectedCandidates; } set { drawRejectedCandidates = value; } }
      public bool DrawAxes { get { return drawAxes; } set { drawAxes = value; } }
      public bool DrawDetectedCharucoMarkers { get { return drawDetectedCharucoMarkers; } set { drawDetectedCharucoMarkers = value; } }
      public bool DrawDetectedDiamonds { get { return drawDetectedDiamonds; } set { drawDetectedDiamonds = value; } }
      public ArucoMarkerTracker MarkerTracker { get; protected set; }

      // Properties

      /// <summary>
      /// Gets or sets the camera system to use.
      /// </summary>
      public ArucoCamera ConcreteArucoCamera { get { return arucoCamera; } set { arucoCamera = value; } }

      /// <summary>
      /// Gets or sets the undistortion process associated with the ArucoCamera.
      /// </summary>
      public ArucoCameraUndistortion ConcreteArucoCameraUndistortion { get { return arucoCameraUndistortion; } set { arucoCameraUndistortion = value; } }

      /// <summary>
      /// Gets or sets the optional camera display associated with the ArucoCamera.
      /// </summary>
      public ArucoCameraGenericDisplay ConcreteArucoCameraDisplay { get { return arucoCameraDisplay; } set { arucoCameraDisplay = value; } }

      // Variables

      protected Dictionary<Type, ArucoObjectTracker> additionalTrackers;
      protected ArucoCameraSeparateThread trackingThread;

      // MonoBehaviour methods

      /// <summary>
      /// Initializes the trackers list.
      /// </summary>
      protected override void Awake()
      {
        base.Awake();
        
        MarkerTracker = new ArucoMarkerTracker();
        additionalTrackers = new Dictionary<Type, ArucoObjectTracker>()
        {
          { typeof(ArucoGridBoard), new ArucoGridBoardTracker() },
          { typeof(ArucoCharucoBoard), new ArucoCharucoBoardTracker() },
          { typeof(ArucoDiamond), new ArucoDiamondTracker() }
        };
      }

      // ArucoCameraController methods

      /// <summary>
      /// Setups controller dependencies.
      /// </summary>
      public override void Configure()
      {
        base.Configure();

        ControllerDependencies.Add(ArucoCameraUndistortion);
        if (ArucoCameraDisplay != null)
        {
          ControllerDependencies.Add(ArucoCameraDisplay);
        }

        OnConfigured();
      }

      /// <summary>
      /// Initializes the tracking, activates the trackers, susbcribes to the <see cref="ArucoObjectsController{T}.ArucoObjectAdded"/> and
      /// <see cref="ArucoObjectsController{T}.ArucoObjectRemoved"/> events and starts the tracking thread.
      /// </summary>
      public override void StartController()
      {
        base.StartController();
        
        MarkerTracker.Activate(this);
        foreach (var arucoObjectDictionary in ArucoObjects)
        {
          foreach (var arucoObject in arucoObjectDictionary.Value)
          {
            ArucoObjectsController_ArucoObjectAdded(arucoObject.Value);
          }
        }
        
        ArucoObjectAdded += ArucoObjectsController_ArucoObjectAdded;
        ArucoObjectRemoved += ArucoObjectsController_ArucoObjectRemoved;

        trackingThread = new ArucoCameraSeparateThread(ArucoCamera, UpdateArucoObjects, TrackArucoObjects,
          () => { StopController(); });
        trackingThread.Start();

        OnStarted();
      }

      /// <summary>
      /// Unsuscribes from ArucoObjectController events, deactivates the trackers and abort the tracking thread and stops the tracking thread.
      /// </summary>
      public override void StopController()
      {
        base.StopController();

        trackingThread.Stop();

        ArucoObjectAdded -= ArucoObjectsController_ArucoObjectAdded;
        ArucoObjectRemoved -= ArucoObjectsController_ArucoObjectRemoved;

        MarkerTracker.Deactivate();
        foreach (var tracker in additionalTrackers)
        {
          if (tracker.Value.IsActivated)
          {
            tracker.Value.Deactivate();
          }
        }

        OnStopped();
      }

      // ArucoObjectController methods

      /// <summary>
      /// Activates the tracker associated with the <paramref name="arucoObject"/> and configure its gameObject.
      /// </summary>
      /// <param name="arucoObject">The added ArUco object.</param>
      protected virtual void ArucoObjectsController_ArucoObjectAdded(ArucoObject arucoObject)
      {
        if (arucoObject.GetType() != typeof(ArucoMarker))
        {
          ArucoObjectTracker tracker = null;
          if (!additionalTrackers.TryGetValue(arucoObject.GetType(), out tracker))
          {
            throw new ArgumentException("No tracker found for the type '" + arucoObject.GetType() + "'.", "arucoObject");
          }
          else if (!tracker.IsActivated)
          {
            tracker.Activate(this);
          }
        }
      }

      /// <summary>
      /// Deactivates the tracker associated with the <paramref name="arucoObject"/> if it was the last one of this type.
      /// </summary>
      /// <param name="arucoObject">The removed</param>
      protected virtual void ArucoObjectsController_ArucoObjectRemoved(ArucoObject arucoObject)
      {
        ArucoObjectTracker tracker = null;
        if (arucoObject.GetType() == typeof(ArucoMarker) || !additionalTrackers.TryGetValue(arucoObject.GetType(), out tracker))
        {
          return;
        }

        if (tracker.IsActivated)
        {
          bool deactivateTracker = true;

          // Try to find at leat one object of the same type as arucoObject
          foreach (var arucoObjectDictionary in ArucoObjects)
          {
            foreach (var arucoObject2 in arucoObjectDictionary.Value)
            {
              if (arucoObject2.GetType() == arucoObject.GetType())
              {
                deactivateTracker = false;
                break;
              }
            }
            if (!deactivateTracker)
            {
              break;
            }
          }

          if (deactivateTracker)
          {
            tracker.Deactivate();
          }
        }
      }

      // IArucoObjectsTracker Methods

      public void DeactivateArucoObjects()
      {
        foreach (var arucoObjectDictionary in ArucoObjects)
        {
          foreach (var arucoObject in arucoObjectDictionary.Value)
          {
            arucoObject.Value.gameObject.SetActive(false);
          }
        }
      }

      public void Detect(Cv.Mat[] images)
      {
        if (!IsConfigured)
        {
          throw new Exception("The tracker must be configured before tracking ArUco objects.");
        }

        ExecuteOnActivatedTrackers((tracker, cameraId, dictionary) =>
        {
          tracker.Detect(cameraId, dictionary, images[cameraId]);
        });
      }

      public void Detect()
      {
        Detect(ArucoCamera.Images);
      }

      public void Draw(Cv.Mat[] images)
      {
        if (!IsConfigured)
        {
          throw new Exception("The tracker must be configured before tracking ArUco objects.");
        }

        ExecuteOnActivatedTrackers((tracker, cameraId, dictionary) =>
        {
          tracker.Draw(cameraId, dictionary, images[cameraId]);
        });
      }

      public void Draw()
      {
        Draw(ArucoCamera.Images);
      }

      public void EstimateTransforms()
      {
        if (!IsConfigured)
        {
          throw new Exception("The tracker must be configured before tracking ArUco objects.");
        }

        ExecuteOnActivatedTrackers((tracker, cameraId, dictionary) =>
        {
          tracker.EstimateTransforms(cameraId, dictionary);
        });
      }

      public void UpdateTransforms()
      {
        if (!IsConfigured)
        {
          throw new Exception("The tracker must be configured before tracking ArUco objects.");
        }

        ExecuteOnActivatedTrackers((tracker, cameraId, dictionary) =>
        {
          tracker.UpdateTransforms(cameraId, dictionary);
        });
      }

      // Methods

      /// <summary>
      /// Detects and estimates the transforms of the detected ArUco objects. Executed on a separated tracking thread.
      /// </summary>
      protected void TrackArucoObjects()
      {
        Detect(trackingThread.Images);
        EstimateTransforms();
        Draw(trackingThread.Images);
      }

      /// <summary>
      /// Calls <see cref="DeactivateArucoObjects"/> and <see cref="UpdateTransforms"/>.
      /// </summary>
      protected void UpdateArucoObjects()
      {
        DeactivateArucoObjects();
        if (ArucoCameraDisplay != null)
        {
          UpdateTransforms();
        }
      }

      /// <summary>
      /// Executes an <paramref name="actionOnTracker"/> on all the activated <see cref="ArucoObjectTracker"/>.
      /// </summary>
      protected void ExecuteOnActivatedTrackers(Action<ArucoObjectTracker, int, Aruco.Dictionary> actionOnTracker)
      {
        for (int cameraId = 0; cameraId < ArucoCamera.CameraNumber; cameraId++)
        {
          foreach (var arucoObjectDictionary in ArucoObjects)
          {
            actionOnTracker(MarkerTracker, cameraId, arucoObjectDictionary.Key);
            foreach (var tracker in additionalTrackers)
            {
              if (tracker.Value.IsActivated)
              {
                actionOnTracker(tracker.Value, cameraId, arucoObjectDictionary.Key);
              }
            }
          }
        }
      }
    }
  }

  /// \} aruco_unity_package
}