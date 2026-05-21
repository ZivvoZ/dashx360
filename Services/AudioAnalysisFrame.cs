namespace XboxMetroLauncher.Services;

public readonly record struct AudioAnalysisFrame(
    double Bass,
    double Mid,
    double Treble,
    double Loudness,
    double Peak);
