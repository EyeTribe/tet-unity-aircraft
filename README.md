AirCraft Unity Sample for the EyeTribe Dev Kit
====
<p>

Introduction
----

With the release of [Unity 5](http://unity3d.com/) came a new set of sample projects. This Unity eye tracking sample is based on the *Standard Assets* of Unity 5 and uses eye tracking to control a flight simulator.

The demo allows the user to calibrate the EyeTribe Dev Kit. Once calibrated, the user can control an aircraft using a gaze indicator. The aircraft follows the laws of physics making it challenging to steer through the obstacles of the game scene using your eyes!


![](http://theeyetribe.com/github/unity_aircraft.png)


Please use our [public forum](http://theeyetribe.com/forum) for questions and support.


Modifications
----

Changes to the original script files are tagged with /* @TheEyeTribe */ in the source files.

Scenes and assets related to calibration are found in the *./Assetts/TheEyeTribe* folder

The *EyeTribeUnityScript* was attached top the Main Camera of the game scene.

Minor change in *Build Settings* was required to use [EyeTribe C# SDK](https://github.com/EyeTribe/tet-csharp-client). *Player Settings -> Windows -> Other Setting -> Api Compatibility Level* must be set to .NET 2.0


Dependencies
----

This sample has been developed in Unity 5.0 and uses the [EyeTribe C# SDK](https://github.com/EyeTribe/tet-csharp-client). 


Build
----

To build, open project in [Unity](http://unity3d.com/) and build for Windows OS or Mac OSX.

Note that the EyeTribe Server currently supports Windows 7 and newer as well as Mac OSX 10.8 and never. Support for other platforms will be added in the future.


FAQ
----

Should question arise, do not hesitate to post them on [The Eye Tribe Forum](http://theeyetribe.com/forum/).


Changelog
----

0.9.56 (2015-03-16)
- Initial release
