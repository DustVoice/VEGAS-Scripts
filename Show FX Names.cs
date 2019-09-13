/**
 * LICENSE NOTICE FROM THE ORIGINAL AUTHOR:
 * As I want you to profit from this plugin, I don't want to bother you with restrictive Licenses.
 * That's why you're free to use this script even in any commercial product.
 * Of course I would be more than happy about you mentioning me in your final product, but you're not obliged to.
 * If you're modifying this script however, you have to credit me as the original author.
 * Furthermore, this license notice mustn't be emitted or changed. You can add your own notice to this section but you are not allowed to remove it.
 * If this script gets more attention than anticipated I will consider removing this notice altogether, but it's my decision to do so.
 * Also I am not to be held liable if you use my script and something bad happens. Use at your own risk!
 *
 * The original author is:
 * =======================
 * David Holland
 * aka DustVoice
 *
 * info@dustvoice.de
 * https://dustvoice.de
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using ScriptPortal.Vegas;

public class EntryPoint {
    public void FromVegas(Vegas vegas) {
        TrackEvent[] tes = FindSelectedEvents(vegas.Project);
        VideoEvent[] ves = GetVideoEvents(tes);

        if (ves.Length != 1)
        {
            MessageBox.Show("You have to select exactly 1 video events (in no particular order) in order for this script to work.\n\nTerminating...", "Wrong selections", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        VideoEvent ve = ShowFXNames(ves);

    }

    private TrackEvent[] FindSelectedEvents(Project project)
    {
        List<TrackEvent> selectedEvents = new List<TrackEvent>();

        foreach (Track track in project.Tracks)
        {
            foreach (TrackEvent trackEvent in track.Events)
            {
                if (trackEvent.Selected)
                {
                    selectedEvents.Add(trackEvent);
                }
            }
        }
        return selectedEvents.ToArray();
    }

    private VideoEvent[] GetVideoEvents(TrackEvent[] te)
    {
        List<VideoEvent> videoEvents = new List<VideoEvent>();

        foreach (TrackEvent trackEvent in te)
        {
            if (trackEvent.IsVideo())
            {
                videoEvents.Add((VideoEvent) trackEvent);
            }
        }

        return videoEvents.ToArray();
    }

    private VideoEvent ShowFXNames(VideoEvent[] ve)
    {
        string effectNames = new string();
        foreach (VideoEvent videoEvent in ve)
        {
            foreach (Effect fx in videoEvent.Effects)
            {
                effectNames = fx.Description + "\n";
            }
        }

        MessageBox.Show(effectNames, "Concrete effect names", MessageBoxButtons.OK, MessageBoxIcon.Information);

        return null;
    }
}
