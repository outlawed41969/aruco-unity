﻿using ArucoUnity.Cameras.Parameters;
using ArucoUnity.Cameras.Calibrations.Flags;
using ArucoUnity.Objects;
using ArucoUnity.Plugin;
using System;
using System.Threading;
using UnityEngine;
using ArucoUnity.Controllers;

namespace ArucoUnity
{
  /// \addtogroup aruco_unity_package
  /// \{

  namespace Cameras.Calibrations
  {
    /// <summary>
    /// Calibrates a <see cref="IArucoCamera"/> with a <see cref="ArucoBoard"/> and saves the calibrated camera parameters in a file managed by
    /// <see cref="CameraParametersController"/>.
    /// 
    /// See the OpenCV and the ArUco module documentations for more information about the calibration process:
    /// http://docs.opencv.org/3.3.0/da/d13/tutorial_aruco_calibration.html and https://docs.opencv.org/3.3.0/da/d13/tutorial_aruco_calibration.html
    /// </summary>
    public abstract class ArucoCameraCalibration : ArucoObjectDetector
    {
      // Editor fields

      [SerializeField]
      [Tooltip("The ArUco board to use for calibration.")]
      private ArucoBoard calibrationBoard;

      [SerializeField]
      [Tooltip("Use a refine algorithm to find not detected markers based on the already detected and the board layout (if using a board).")]
      private bool refineMarkersDetection = false;

      [SerializeField]
      [Tooltip("The camera parameters to use if CalibrationFlags.UseIntrinsicGuess is true. Otherwise, the camera parameters file will be generated" +
        " from the camera name and the calibration datetime.")]
      private CameraParametersController cameraParametersController;

      // Properties

      /// <summary>
      /// Gets or sets the ArUco board to use for calibration.
      /// </summary>
      public ArucoBoard CalibrationBoard { get { return calibrationBoard; } set { calibrationBoard = value; } }

      /// <summary>
      /// Gets or sets if need to use a refine algorithm to find not detected markers based on the already detected and the board layout.
      /// </summary>
      public bool RefineMarkersDetection { get { return refineMarkersDetection; } set { refineMarkersDetection = value; } }

      /// <summary>
      /// Gets or sets the camera parameters to use if <see cref="CameraCalibrationFlags.UseIntrinsicGuess"/> is true. Otherwise, the camera parameters
      /// file will be generated from the camera name and the calibration datetime.
      /// </summary>
      public CameraParametersController CameraParametersController { get { return cameraParametersController; } set { cameraParametersController = value; } }

      /// <summary>
      /// Gets the detected marker corners for each camera.
      /// </summary>
      public Std.VectorVectorVectorPoint2f[] MarkerCorners { get; protected set; }

      /// <summary>
      /// Gets the detected marker ids for each camera.
      /// </summary>
      public Std.VectorVectorInt[] MarkerIds { get; protected set; }

      /// <summary>
      /// Gets the images to use for the calibration.
      /// </summary>
      public Std.VectorMat[] CalibrationImages { get; protected set; }

      /// <summary>
      /// Gets the estimated rotation vector for each detected markers in each camera.
      /// </summary>
      public Std.VectorVec3d[] Rvecs { get; protected set; }

      /// <summary>
      /// Gets the estimated translation vector for each detected markers in each camera.
      /// </summary>
      public Std.VectorVec3d[] Tvecs { get; protected set; }

      /// <summary>
      /// Gets the detected marker corners on the current images of each camera.
      /// </summary>
      public Std.VectorVectorPoint2f[] MarkerCornersCurrentImage { get; protected set; }

      /// <summary>
      /// Gets the detected marker ids on the current images of each camera.
      /// </summary>
      public Std.VectorInt[] MarkerIdsCurrentImage { get; protected set; }

      /// <summary>
      /// Gets if the last <see cref="CalibrateAsync"/> call has been a success.
      /// </summary>
      public bool IsCalibrated { get; protected set; }

      /// <summary>
      /// Gets if <see cref="CalibrateAsync"/> has been called and hasn't completed yet.
      /// </summary>
      public bool CalibrationRunning { get; protected set; }

      // Events

      /// <summary>
      /// Called when <see cref="IsCalibrated"/> is set to true.
      /// </summary>
      public event Action Calibrated = delegate { };

      // Variables
      
      protected string applicationPath;
      protected Cv.Size[] calibrationImageSizes;
      protected Thread calibratingThread;
      protected Mutex calibratingMutex = new Mutex();
      protected Exception calibratingException;

      // MonoBehaviour methods

      /// <summary>
      /// Calls the <see cref="Calibrated"/> event when a calibration has just completed.
      /// </summary>
      protected virtual void LateUpdate()
      {
        Exception e = null;
        bool calibrationDone = false;
        calibratingMutex.WaitOne();
        {
          e = calibratingException;
          calibratingException = null;

          calibrationDone = CalibrationRunning && IsCalibrated;
        }
        calibratingMutex.ReleaseMutex();

        // Check for exception in calibrating thread
        if (e != null)
        {
          calibratingThread.Abort();
          CalibrationRunning = false;
          throw e;
        }

        // Check for calibration done
        if (calibrationDone)
        {
          CalibrationRunning = false;
          Calibrated.Invoke();
        }
      }

      // ArucoCameraController methods

      /// <summary>
      /// Checks if <see cref="CalibrationBoard"/> is set and calls <see cref="ResetCalibration"/>.
      /// </summary>
      public override void Configure()
      {
        base.Configure();

        if (CalibrationBoard == null)
        {
          throw new ArgumentNullException("CalibrationBoard", "This property needs to be set to configure the calibration controller.");
        }

        ResetCalibration();
        OnConfigured();
      }

      /// <summary>
      /// Susbcribes to <see cref="ArucoCamera.UndistortRectifyImages"/>.
      /// </summary>
      public override void StartController()
      {
        base.StartController();
        ArucoCamera.ImagesUpdated += ArucoCamera_ImagesUpdated;
        OnStarted();
      }

      /// <summary>
      /// Unsusbcribes from <see cref="ArucoCamera.UndistortRectifyImages"/>.
      /// </summary>
      public override void StopController()
      {
        base.StopController();
        ArucoCamera.ImagesUpdated -= ArucoCamera_ImagesUpdated;
        OnStopped();
      }

      /// <summary>
      /// Detects and draw the ArUco markers on the current images of the cameras.
      /// </summary>
      protected virtual void ArucoCamera_ImagesUpdated()
      {
        DetectMarkers();
        DrawDetectedMarkers();
      }

      // Methods

      /// <summary>
      /// Resets the properties.
      /// </summary>
      public virtual void ResetCalibration()
      {
        MarkerCorners = new Std.VectorVectorVectorPoint2f[ArucoCamera.CameraNumber];
        MarkerIds = new Std.VectorVectorInt[ArucoCamera.CameraNumber];
        CalibrationImages = new Std.VectorMat[ArucoCamera.CameraNumber];
        for (int cameraId = 0; cameraId < ArucoCamera.CameraNumber; cameraId++)
        {
          MarkerCorners[cameraId] = new Std.VectorVectorVectorPoint2f();
          MarkerIds[cameraId] = new Std.VectorVectorInt();
          CalibrationImages[cameraId] = new Std.VectorMat();
        }
        
        Rvecs = new Std.VectorVec3d[ArucoCamera.CameraNumber];
        Tvecs = new Std.VectorVec3d[ArucoCamera.CameraNumber];
        MarkerCornersCurrentImage = new Std.VectorVectorPoint2f[ArucoCamera.CameraNumber];
        MarkerIdsCurrentImage = new Std.VectorInt[ArucoCamera.CameraNumber];

        calibrationImageSizes = new Cv.Size[ArucoCamera.CameraNumber];

        IsCalibrated = false;
      }

      /// <summary>
      /// Detects the Aruco markers on the current images of the cameras and store the results in the <see cref="MarkerCornersCurrentImage"/> and
      /// <see cref="MarkerIdsCurrentImage"/> properties.
      /// </summary>
      public virtual void DetectMarkers()
      {
        if (!IsConfigured)
        {
          throw new Exception("Configure the calibration controller before detect markers.");
        }

        for (int cameraId = 0; cameraId < ArucoCamera.CameraNumber; cameraId++)
        {
          Std.VectorInt markerIds;
          Std.VectorVectorPoint2f markerCorners, rejectedCandidateCorners;

          Cv.Mat image = ArucoCamera.Images[cameraId];

          Aruco.DetectMarkers(image, CalibrationBoard.Dictionary, out markerCorners, out markerIds, DetectorParameters, out rejectedCandidateCorners);

          MarkerCornersCurrentImage[cameraId] = markerCorners;
          MarkerIdsCurrentImage[cameraId] = markerIds;

          if (RefineMarkersDetection)
          {
            Aruco.RefineDetectedMarkers(image, CalibrationBoard.Board, MarkerCornersCurrentImage[cameraId], MarkerIdsCurrentImage[cameraId],
              rejectedCandidateCorners);
          }
        }
      }

      /// <summary>
      /// Draws the detected ArUco markers on the current images of the cameras.
      /// </summary>
      public virtual void DrawDetectedMarkers()
      {
        if (!IsConfigured)
        {
          throw new Exception("Configure the calibration controller before drawing detected markers.");
        }

        for (int cameraId = 0; cameraId < ArucoCamera.CameraNumber; cameraId++)
        {
          if (MarkerIdsCurrentImage[cameraId] != null && MarkerIdsCurrentImage[cameraId].Size() > 0)
          {
            Aruco.DrawDetectedMarkers(ArucoCamera.Images[cameraId], MarkerCornersCurrentImage[cameraId], MarkerIdsCurrentImage[cameraId]);
          }
        }
      }

      /// <summary>
      /// Adds the current images of the cameras and the detected corners for the calibration.
      /// </summary>
      public virtual void AddCurrentFrameForCalibration()
      {
        if (!IsConfigured)
        {
          throw new Exception("Configure the calibration controller before adding the current frame for calibration.");
        }

        // Check for validity
        uint markerIdsNumber = (MarkerIdsCurrentImage[0] != null) ? MarkerIdsCurrentImage[0].Size() : 0;
        for (int cameraId = 0; cameraId < ArucoCamera.CameraNumber; cameraId++)
        {
          if (MarkerIdsCurrentImage[cameraId] == null || MarkerIdsCurrentImage[cameraId].Size() < 1)
          {
            throw new Exception("No markers detected for the camera " + (cameraId + 1) + "/" + ArucoCamera.CameraNumber + " to add the"
              + " current images for the calibration. At least one marker detected is required for calibrating the camera.");
          }

          if (markerIdsNumber != MarkerIdsCurrentImage[cameraId].Size())
          {
            throw new Exception("The cameras must have detected the same number of markers to add the current images for the calibration.");
          }
        }

        // Save the images and the detected corners
        Cv.Mat[] cameraImages = ArucoCamera.Images;
        for (int cameraId = 0; cameraId < ArucoCamera.CameraNumber; cameraId++)
        {
          MarkerCorners[cameraId].PushBack(MarkerCornersCurrentImage[cameraId]);
          MarkerIds[cameraId].PushBack(MarkerIdsCurrentImage[cameraId]);
          CalibrationImages[cameraId].PushBack(ArucoCamera.Images[cameraId].Clone());

          if (calibrationImageSizes[cameraId] == null)
          {
            calibrationImageSizes[cameraId] = new Cv.Size(ArucoCamera.Images[cameraId].Size.Width, ArucoCamera.Images[cameraId].Size.Height);
          }
        }
      }

      /// <summary>
      /// Calls <see cref="Calibrate"/> in a background thread.
      /// </summary>
      public virtual void CalibrateAsync()
      {
        if (!IsConfigured)
        {
          throw new Exception("Configure the calibration controller before starting the async calibration.");
        }

        bool calibrationRunning = false;
        calibratingMutex.WaitOne();
        {
          calibrationRunning = CalibrationRunning;
        }
        calibratingMutex.ReleaseMutex();

        if (calibrationRunning)
        {
          throw new Exception("A calibration is already running. Wait its completion or call CancelCalibrateAsync() before starting a new calibration.");
        }

        calibratingThread = new Thread(() =>
        {
          try
          {
            Calibrate();
          }
          catch (Exception e)
          {
            calibratingMutex.WaitOne();
            {
              calibratingException = e;
            }
            calibratingMutex.ReleaseMutex();
          }
        });
        calibratingThread.IsBackground = true;
        calibratingThread.Start();
      }

      /// <summary>
      /// Stops the calibration if <see cref="CalibrationRunning"/> is true.
      /// </summary>
      public virtual void CancelCalibrateAsync()
      {
        if (!IsConfigured)
        {
          throw new Exception("Configure the calibration controller before starting or canceling the calibration.");
        }

        bool calibrationRunning = false;
        calibratingMutex.WaitOne();
        {
          calibrationRunning = CalibrationRunning;
        }
        calibratingMutex.ReleaseMutex();

        if (!calibrationRunning)
        {
          throw new Exception("Start the async calibration before canceling it.");
        }

        calibratingThread.Abort();
      }

      /// <summary>
      /// Calibrates each camera of the <see cref="ArucoObjectDetector.ArucoCamera"/> system using the detected markers added with
      /// <see cref="AddCurrentFrameForCalibration()"/>, the <see cref="CameraParameters"/>, the <see cref="ArucoCameraUndistortion"/> and save
      /// the results on a calibration file. Stereo calibrations will be additionally executed on these results for every camera pair in
      /// <see cref="StereoCalibrationCameraPairs"/>.
      /// </summary>
      public virtual void Calibrate()
      {
        if (!IsConfigured)
        {
          throw new Exception("Configure the calibration controller before starting the calibration.");
        }

        // Update state
        calibratingMutex.WaitOne();
        {
          IsCalibrated = false;
          CalibrationRunning = true;
        }
        calibratingMutex.ReleaseMutex();

        // Check if there is enough captured frames for calibration
        Aruco.CharucoBoard charucoBoard = CalibrationBoard.Board as Aruco.CharucoBoard;
        for (int cameraId = 0; cameraId < ArucoCamera.CameraNumber; cameraId++)
        {
          if (charucoBoard == null && MarkerIds[cameraId].Size() < 3)
          {
            throw new Exception("Need at least three frames captured for the camera " + (cameraId + 1) + "/" + ArucoCamera.CameraNumber
              + " to calibrate.");
          }
          else if (charucoBoard != null && MarkerIds[cameraId].Size() < 4)
          {
            throw new Exception("Need at least four frames captured for the camera " + (cameraId + 1) + "/" + ArucoCamera.CameraNumber
              + " to calibrate with a ChAruco board.");
          }
        }

        InitializeCameraParameters(); // Initialize and configure the camera parameters

        // Get objet and image calibration points from detected ids and corners
        Std.VectorVectorPoint2f[] imagePoints = new Std.VectorVectorPoint2f[ArucoCamera.CameraNumber];
        Std.VectorVectorPoint3f[] objectPoints = new Std.VectorVectorPoint3f[ArucoCamera.CameraNumber];
        for (int cameraId = 0; cameraId < ArucoCamera.CameraNumber; cameraId++)
        {
          imagePoints[cameraId] = new Std.VectorVectorPoint2f();
          objectPoints[cameraId] = new Std.VectorVectorPoint3f();

          uint frameCount = MarkerCorners[cameraId].Size();
          for (uint frame = 0; frame < frameCount; frame++)
          {
            Std.VectorPoint2f frameImagePoints;
            Std.VectorPoint3f frameObjectPoints;

            if (charucoBoard == null)
            {
              // Using a grid board
              Aruco.GetBoardObjectAndImagePoints(CalibrationBoard.Board, MarkerCorners[cameraId].At(frame), MarkerIds[cameraId].At(frame),
                out frameObjectPoints, out frameImagePoints);
            }
            else
            {
              // Using a charuco board
              Std.VectorInt charucoIds;
              Aruco.InterpolateCornersCharuco(MarkerCorners[cameraId].At(frame), MarkerIds[cameraId].At(frame), CalibrationImages[cameraId].At(frame),
                charucoBoard, out frameImagePoints, out charucoIds);

              // Join the object points corresponding to the detected markers
              frameObjectPoints = new Std.VectorPoint3f();
              uint markerCount = charucoIds.Size();
              for (uint marker = 0; marker < markerCount; marker++)
              {
                uint pointId = (uint)charucoIds.At(marker);
                Cv.Point3f objectPoint = charucoBoard.ChessboardCorners.At(pointId);
                frameObjectPoints.PushBack(objectPoint);
              }
            }

            imagePoints[cameraId].PushBack(frameImagePoints);
            objectPoints[cameraId].PushBack(frameObjectPoints);
          }
        }

        // Calibrate the Aruco camera
        Calibrate(imagePoints, objectPoints);

        // Save the camera parameters
        CameraParametersController.CameraParametersFilename = ArucoCamera.Name + " - "
          + CameraParametersController.CameraParameters.CalibrationDateTime.ToString("yyyy-MM-dd_HH-mm-ss") + ".xml";
        CameraParametersController.Save();

        // Update state
        calibratingMutex.WaitOne();
        {
          IsCalibrated = true;
        }
        calibratingMutex.ReleaseMutex();
      }

      /// <summary>
      /// Initializes and configure the <see cref="CameraParametersController.CameraParameters"/>.
      /// </summary>
      /// <param name="calibrationFlags">The calibration flags that will be used in <see cref="Calibrate"/>.</param>
      protected virtual void InitializeCameraParameters(CameraCalibrationFlags calibrationFlags = null)
      {
        if (calibrationFlags != null && calibrationFlags.UseIntrinsicGuess)
        {
          if (CameraParametersController.CameraParameters == null || CameraParametersController.CameraParameters.CameraMatrices == null)
          {
            throw new Exception("CalibrationFlags.UseIntrinsicGuess flag is set but CameraParameters is null or has no valid values. Set" +
              " CameraParametersFilename or deactivate this flag.");
          }
        }
        else
        {
          CameraParametersController.Initialize(ArucoCamera.CameraNumber);
          for (int cameraId = 0; cameraId < ArucoCamera.CameraNumber; cameraId++)
          {
            CameraParametersController.CameraParameters.CameraMatrices[cameraId] = new Cv.Mat();
            CameraParametersController.CameraParameters.DistCoeffs[cameraId] = new Cv.Mat();
            CameraParametersController.CameraParameters.OmnidirXis[cameraId] = new Cv.Mat();
          }
        }

        CameraParametersController.CameraParameters.CalibrationFlagsValue = (calibrationFlags != null) ? calibrationFlags.CalibrationFlagsValue : default(int);

        for (int cameraId = 0; cameraId < ArucoCamera.CameraNumber; cameraId++)
        {
          CameraParametersController.CameraParameters.ImageHeights[cameraId] = calibrationImageSizes[cameraId].Height;
          CameraParametersController.CameraParameters.ImageWidths[cameraId] = calibrationImageSizes[cameraId].Width;
        }
      }

      /// <summary>
      /// Applies a calibration to the <see cref="ArucoCameraController.ArucoCamera"/>, set the extrinsic camera parameters to <see cref="Rvecs"/>
      /// and <see cref="Tvecs"/> and saves the camera parameters in <see cref="CameraParametersController.CameraParameters"/>.
      /// </summary>
      /// <param name="imagePoints">The detected image points of each camera.</param>
      /// <param name="objectPoints">The corresponding object points of each camera.</param>
      protected abstract void Calibrate(Std.VectorVectorPoint2f[] imagePoints, Std.VectorVectorPoint3f[] objectPoints);
    }
  }

  /// \} aruco_unity_package
}