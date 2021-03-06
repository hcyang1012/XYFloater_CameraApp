﻿using System;
using Android.App;
using Android.Content;
using Android.Widget;
using Android.OS;
using Java.IO;
using Android.Graphics;
using System.Collections.Generic;
using Android.Provider;
using Android.Content.PM;
using OpenCV.ImgProc;
using OpenCV.Android;
using Android.Util;
using OpenCV.Core;

namespace XYFloater_CameraApp
{
    public enum Filter_Type { CANNY, SOBEL };
    [Activity(Label = "XYFloater_CameraApp", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        

        private ImageView _imageView = null;
        private ImageView _edgeDetectedView = null;

        public const string TAG = "XYFloater_Camera::Activity";
        private BaseLoaderCallback mLoaderCallback;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            // Get our button from the layout resource,
            // and attach an event to it

            mLoaderCallback = new Callback(this);
            if (IsThereAnAppToTakePictures())
            {
                CreateDirectoryForPictures();

                Button button = FindViewById<Button>(Resource.Id.myButton);
                _imageView = FindViewById<ImageView>(Resource.Id.imageView1);
                _edgeDetectedView = FindViewById<ImageView>(Resource.Id.imageView2);
                button.Click += TakeAPicture;
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (!OpenCVLoader.InitDebug())
            {
                Log.Debug(TAG, "Internal OpenCV library not found. Using OpenCV Manager for initialization");
                OpenCVLoader.InitAsync(OpenCVLoader.OpencvVersion300, this, mLoaderCallback);
            }
            else
            {
                Log.Debug(TAG, "OpenCV library found inside package. Using it!");
                mLoaderCallback.OnManagerConnected(LoaderCallbackInterface.Success);
            }
        }

        private void CreateDirectoryForPictures()
        {
            App._dir = new File(
                Android.OS.Environment.GetExternalStoragePublicDirectory(
                    Android.OS.Environment.DirectoryPictures), "CameraAppDemo");
            if (!App._dir.Exists())
            {
                App._dir.Mkdirs();
            }
        }

        private bool IsThereAnAppToTakePictures()
        {
            Intent intent = new Intent(MediaStore.ActionImageCapture);
            IList<ResolveInfo> availableActivities =
                PackageManager.QueryIntentActivities(intent, PackageInfoFlags.MatchDefaultOnly);
            return availableActivities != null && availableActivities.Count > 0;
        }

        private void TakeAPicture(object sender, EventArgs eventArgs)
        {
            Intent intent = new Intent(MediaStore.ActionImageCapture);
            App._file = new File(App._dir, String.Format("myPhoto_{0}.jpg", Guid.NewGuid()));
            intent.PutExtra(MediaStore.ExtraOutput, Android.Net.Uri.FromFile(App._file));
            StartActivityForResult(intent, 0);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            // Make it available in the gallery

            Intent mediaScanIntent = new Intent(Intent.ActionMediaScannerScanFile);
            Android.Net.Uri contentUri = Android.Net.Uri.FromFile(App._file);
            mediaScanIntent.SetData(contentUri);
            SendBroadcast(mediaScanIntent);

            // Display in ImageView. We will resize the bitmap to fit the display.
            // Loading the full sized image will consume to much memory
            // and cause the application to crash.

            int height = Resources.DisplayMetrics.HeightPixels;
            int width = _imageView.Height;
            App.bitmap = App._file.Path.LoadAndResizeBitmap(width, height);
            if (App.bitmap != null)
            {
                App.edgeDetectdBitmap = App.bitmap.getEdgeDetectedImage(Filter_Type.CANNY);
                _imageView.SetImageBitmap(App.edgeDetectdBitmap);

                App.edgeDetectdBitmap = App.bitmap.getEdgeDetectedImage(Filter_Type.SOBEL);
                _edgeDetectedView.SetImageBitmap(App.edgeDetectdBitmap);

                App.grayBitmap = null;
                App.bitmap = null;
                App.edgeDetectdBitmap = null;
            }

            // Dispose of the Java side bitmap.
            GC.Collect();
        }
    }

    public static class App
    {
        public static File _file;
        public static File _dir;
        public static Bitmap bitmap;
        public static Bitmap grayBitmap;
        public static Bitmap edgeDetectdBitmap;
    }

    public static class BitmapHelpers
    {
        public static Bitmap LoadAndResizeBitmap(this string fileName, int width, int height)
        {
            // First we get the the dimensions of the file on disk
            BitmapFactory.Options options = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeFile(fileName, options);

            // Next we calculate the ratio that we need to resize the image by
            // in order to fit the requested dimensions.
            int outHeight = options.OutHeight;
            int outWidth = options.OutWidth;
            int inSampleSize = 1;

            if (outHeight > height || outWidth > width)
            {
                inSampleSize = outWidth > outHeight
                                   ? outHeight / height
                                   : outWidth / width;
            }

            // Now we will load the image and have BitmapFactory resize it for us.
            options.InSampleSize = inSampleSize;
            options.InJustDecodeBounds = false;
            Bitmap resizedBitmap = BitmapFactory.DecodeFile(fileName, options);

            return resizedBitmap;
        }



        public static Bitmap toGrayScale(this string fileName)
        {
            BitmapFactory.Options options = new BitmapFactory.Options { InPreferredConfig = Bitmap.Config.Argb8888 };
            Bitmap originalBitmap = BitmapFactory.DecodeFile(fileName, options);

            Bitmap grayBitmap = Bitmap.CreateBitmap(options.OutWidth, options.OutHeight, originalBitmap.GetConfig());
            Canvas canvas = new Canvas(grayBitmap);
            Paint paint = new Paint();
            ColorMatrix colorMatrix = new ColorMatrix();
            colorMatrix.SetSaturation(0);
            ColorMatrixColorFilter colorFilter = new ColorMatrixColorFilter(colorMatrix);
            paint.SetColorFilter(colorFilter);
            canvas.DrawBitmap(originalBitmap, 0, 0, paint);

            return grayBitmap;
        }

        public static Bitmap getEdgeDetectedImage(this Bitmap src, Filter_Type filter_type)
        {
            
            Bitmap resizedBitmap = Bitmap.CreateScaledBitmap(src, (src.Width * 256) / src.Height, 256, true);

            OpenCV.Core.Mat resizedMat = new OpenCV.Core.Mat();
            OpenCV.Android.Utils.BitmapToMat(resizedBitmap, resizedMat);

            OpenCV.Core.Mat gaussianMat = new OpenCV.Core.Mat();
            Imgproc.GaussianBlur(resizedMat, gaussianMat, new OpenCV.Core.Size(3, 3), 0, 0);


            OpenCV.Core.Mat grayMat = new OpenCV.Core.Mat();
            Imgproc.CvtColor(gaussianMat, grayMat, Imgproc.ColorRgba2gray, 2);

            OpenCV.Core.Mat edgeDetectedMat = new OpenCV.Core.Mat();
            if (filter_type == Filter_Type.CANNY)
            {

                Imgproc.Canny(grayMat, edgeDetectedMat, 100, 100);
            }
            else
            {
                OpenCV.Core.Mat sobelMat = new OpenCV.Core.Mat();
                Imgproc.Sobel(grayMat, sobelMat, CvType.Cv8u, 1, 1);
                Core.ConvertScaleAbs(sobelMat, edgeDetectedMat, 6, 1);
            }


            Bitmap resultBitmap = Bitmap.CreateBitmap(resizedBitmap.Width, resizedBitmap.Height,Bitmap.Config.Argb8888);
            OpenCV.Android.Utils.MatToBitmap(edgeDetectedMat, resultBitmap);

            return resultBitmap;
        }
    }

    class Callback : BaseLoaderCallback
    {

        public Callback(Context context)
            : base(context)
        {


        }

        public override void OnManagerConnected(int status)
        {
            switch (status)
            {
                case LoaderCallbackInterface.Success:
                    {
                        Log.Info(MainActivity.TAG, "OpenCV loaded successfully");
                    }
                    break;
                default:
                    {
                        base.OnManagerConnected(status);
                    }
                    break;
            }
        }
    }
}

