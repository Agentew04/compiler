namespace FortallCompiler;

public class Profiler {

    private class ProfileData {
        public string Name { get; init; }
        public DateTime StartTime { get; init; }
        public TimeSpan Length { get; set; }
    }

    private List<ProfileData> data = [];
    
    private ProfileData? currentProfile = null;

    public void StartProfile(string name) {
        currentProfile = new ProfileData() {
            Name = name,
            StartTime = DateTime.Now
        };
    }
    
    public void EndProfile() {
        if (currentProfile is null) {
            return;
        }
        currentProfile.Length = DateTime.Now - currentProfile.StartTime;
        
        data.Add(currentProfile);
        currentProfile = null;
    }
}