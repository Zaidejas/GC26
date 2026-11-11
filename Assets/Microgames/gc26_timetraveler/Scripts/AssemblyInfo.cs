using System.Runtime.CompilerServices;

// The automated tests in Tests/ drive the runner through internal hooks (scripted input, track pieces).
[assembly: InternalsVisibleTo("GC26.Timetraveler.Tests")]
