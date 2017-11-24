﻿using ArucoUnity.Cameras;
using ArucoUnity.Controllers.CameraDisplays;
using UnityEngine;

namespace ArucoUnity
{
  /// \addtogroup aruco_unity_package
  /// \{

  namespace Controllers.ObjectTrackers
  {
    public class ArucoObjectsTracker : ArucoObjectsGenericTracker<ArucoCamera>
    {
      // Editor fields

      [SerializeField]
      [Tooltip("The camera display associated with the ArucoCamera.")]
      private ArucoCameraDisplay arucoCameraDisplay;

      // Properties
      
      public override ArucoCameraGenericDisplay<ArucoCamera> ArucoCameraDisplay { get { return arucoCameraDisplay; } }

      /// <summary>
      /// Gets or sets the camera display associated with the ArucoCamera.
      /// </summary>
      public ArucoCameraDisplay ArucoCameraDisplayConcrete { get { return arucoCameraDisplay; } set { arucoCameraDisplay = value; } }
    }
  }

  /// \} aruco_unity_package
}