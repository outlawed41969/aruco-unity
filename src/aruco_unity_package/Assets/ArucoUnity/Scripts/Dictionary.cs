﻿using System.Runtime.InteropServices;

public partial class ArucoUnity
{
  public partial class Dictionary : HandleCvPtr
  {
    [DllImport("ArucoUnity")]
    static extern System.IntPtr au_Dictionary_new1(System.IntPtr bytesList, int markerSize, int maxCorrectionBits);

    [DllImport("ArucoUnity")]
    static extern System.IntPtr au_Dictionary_new2(System.IntPtr bytesList, int markerSize);

    [DllImport("ArucoUnity")]
    static extern System.IntPtr au_Dictionary_new3(System.IntPtr bytesList);

    [DllImport("ArucoUnity")]
    static extern System.IntPtr au_Dictionary_new4();

    [DllImport("ArucoUnity")]
    static extern System.IntPtr au_Dictionary_new5(System.IntPtr dictionary);

    [DllImport("ArucoUnity")]
    static extern void au_Dictionary_delete(System.IntPtr dictionary);

    [DllImport("ArucoUnity")]
    static extern void au_Dictionary_drawMarker(System.IntPtr dictionary, int id, int sidePixels, System.IntPtr img, int borderBits, System.IntPtr exception);

    [DllImport("ArucoUnity")]
    static extern int au_Dictionary_getDistanceToId1(System.IntPtr dictionary, System.IntPtr bits, int id, bool allRotations, System.IntPtr exception);

    [DllImport("ArucoUnity")]
    static extern int au_Dictionary_getDistanceToId2(System.IntPtr dictionary, System.IntPtr bits, int id, System.IntPtr exception);

    [DllImport("ArucoUnity")]
    static extern bool au_Dictionary_identify(System.IntPtr dictionary, System.IntPtr onlyBits, out int idx, out int rotation, double maxCorrectionRate, System.IntPtr exception);

    [DllImport("ArucoUnity")]
    static extern System.IntPtr au_Dictionary_getBitsFromByteList(System.IntPtr byteList, int markerSize, System.IntPtr exception);

    [DllImport("ArucoUnity")]
    static extern System.IntPtr au_Dictionary_getByteListFromBits(System.IntPtr bits);

    [DllImport("ArucoUnity")]
    static extern System.IntPtr au_Dictionary_getBytesList(System.IntPtr dictionary);

    [DllImport("ArucoUnity")]
    static extern int au_Dictionary_getMarkerSize(System.IntPtr dictionary);

    [DllImport("ArucoUnity")]
    static extern int au_Dictionary_getMaxCorrectionBits(System.IntPtr dictionary);

    public Dictionary(Mat bytesList, int markerSize, int maxCorrectionBits) : base(au_Dictionary_new1(bytesList.cvPtr, markerSize, maxCorrectionBits))
    {
    }

    public Dictionary(Mat bytesList, int markerSize) : base(au_Dictionary_new2(bytesList.cvPtr, markerSize))
    {
    }

    public Dictionary(Mat bytesList) : base(au_Dictionary_new3(bytesList.cvPtr))
    {
    }

    public Dictionary() : base(au_Dictionary_new4())
    {
    }

    public Dictionary(Dictionary dictionary) : base(au_Dictionary_new5(dictionary.cvPtr))
    {
    }

    internal Dictionary(System.IntPtr dictionaryPtr) : base(dictionaryPtr)
    {
    }

    ~Dictionary()
    {
      au_Dictionary_delete(cvPtr);
    }
    
    public void DrawMarker(int id, int sidePixels, ref Mat img, int borderBits)
    {
      ArucoUnity.Exception exception = new ArucoUnity.Exception();
      au_Dictionary_drawMarker(cvPtr, id, sidePixels, img.cvPtr, borderBits, exception.cvPtr);
      exception.Check();
    }

    public int getDistanceToId1(Dictionary dictionary, Mat bits, int id, bool allRotations)
    {
      ArucoUnity.Exception exception = new ArucoUnity.Exception();
      int distanceToId = au_Dictionary_getDistanceToId1(cvPtr, bits.cvPtr, id, allRotations, exception.cvPtr);
      exception.Check();
      return distanceToId;
    }

    public int getDistanceToId2(Dictionary dictionary, Mat bits, int id)
    {
      ArucoUnity.Exception exception = new ArucoUnity.Exception();
      int distanceToId = au_Dictionary_getDistanceToId2(cvPtr, bits.cvPtr, id, exception.cvPtr);
      exception.Check();
      return distanceToId;
    }

    public bool identify(Dictionary dictionary, Mat onlyBits, out int idx, out int rotation, double maxCorrectionRate)
    {
      ArucoUnity.Exception exception = new ArucoUnity.Exception();
      bool result = au_Dictionary_identify(cvPtr, onlyBits.cvPtr, out idx, out rotation, maxCorrectionRate, exception.cvPtr);
      exception.Check();
      return result;
    }

    static public Mat getBitsFromByteList(Mat byteList, int markerSiz)
    {
      ArucoUnity.Exception exception = new ArucoUnity.Exception();
      Mat bits = new Mat(au_Dictionary_getBitsFromByteList(byteList.cvPtr, markerSiz, exception.cvPtr));
      exception.Check();
      return bits;
    }

    static public Mat getByteListFromBits(System.IntPtr bits)
    {
      return new Mat(au_Dictionary_getByteListFromBits(bits));
    }

    public Mat bytesList
    {
      get { return new Mat(au_Dictionary_getBytesList(cvPtr)); }
    }

    public int markerSize
    {
      get { return au_Dictionary_getMarkerSize(cvPtr); }
    }

    public int maxCorrectionBits
    {
      get { return au_Dictionary_getMaxCorrectionBits(cvPtr); }
    }

  }
}