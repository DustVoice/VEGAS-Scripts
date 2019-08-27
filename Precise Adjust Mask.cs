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
    const string MOTION_TRACKING_FX_NAME = "VEGAS Bézier Masking";

    const int MASK_0 = 1;
    const int MASK_1 = 2;
    const int MASK_2 = 4;
    const int MASK_3 = 8;
    const int MASK_4 = 16;
    int MASK_BITMASK = 0;

    public void FromVegas(Vegas vegas) {
        Project currProject = vegas.Project;
        Int32 videoWidth = currProject.Video.Width;
        Int32 videoHeight = currProject.Video.Height;

        TrackEvent[] tes = FindSelectedEvents(currProject);
        VideoEvent[] ves = GetVideoEvents(tes);

        if (ves.Length != 1)
        {
            MessageBox.Show("You have to select exactly 1 video events in order for this script to work.\nThe events must contain the \"" + MOTION_TRACKING_FX_NAME + "\" effect with at least one mask enabled. You then zoom in, using pan and crop options. Then after clicking on this script, the pan and crop option will be reset and the point moved, so that it stays on the pixel you selected.\n\nTerminating...", "Wrong selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // foreach (VideoEvent ev in ves)
        // {
        //     foreach (Effect ef in ev.Effects)
        //     {
        //         MessageBox.Show(ef.Description);
        //     }
        // }

        VideoEvent ve = GetEventContainingEffect(ves, MOTION_TRACKING_FX_NAME);

        if (ve == null)
        {
            MessageBox.Show("No selected event with the \"" + MOTION_TRACKING_FX_NAME + "\" plugin found which holds the motion tracking data!\n\nTerminating...", "Wrong selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        OFXEffect fx = GetOFXFromEvent(ve, MOTION_TRACKING_FX_NAME);

        if (fx == null)
        {
            MessageBox.Show("Can't seem to grab the \"" + MOTION_TRACKING_FX_NAME + "\" effect!\n\nTerminating...", "Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            return;
        }

        PopulateMaskBitmask(fx);

        // show_fx_parameters(fx);

        int mask_to_use = PromptForMaskNumber();

        if (mask_to_use == -1)
        {
            return;
        }

        Timecode cursorTime = null;

        Double motionStart = ve.Start.ToMilliseconds();
        Double motionEnd = ve.End.ToMilliseconds();

        Double cursorStart = vegas.Transport.CursorPosition.ToMilliseconds();

        Double max = Math.Max(motionStart, cursorStart);
        Double min = Math.Min(motionEnd, cursorStart);

        if (max != cursorStart || min != cursorStart)
        {
            MessageBox.Show("The cursor must be placed within the event borders!\n\nTerminating...", "Invalid cursor position", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        cursorTime = new Timecode(cursorStart);

        OFXDouble2DParameter motionParam = fx.FindParameterByName("Location_" + mask_to_use.ToString()) as OFXDouble2DParameter;

        if (cursorTime == null)
        {
            MessageBox.Show("Something went wrong as the script tried to determine the cursor position...\n\nTerminating...", "Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            return;
        }

        VideoMotion vevm = ve.VideoMotion;

        VideoMotionKeyframe currKeyframe = new VideoMotionKeyframe(currProject, (cursorTime - ve.Start));
        vevm.Keyframes.Add(currKeyframe);

        Single cutoutWidth = currKeyframe.TopRight.X - currKeyframe.TopLeft.X;
        Single cutoutHeight = currKeyframe.BottomLeft.Y - currKeyframe.TopLeft.Y;
        Single originX = currKeyframe.TopLeft.X;
        Single originY = currKeyframe.BottomLeft.Y;

        OFXDouble2D cursorValue = motionParam.GetValueAtTime(cursorTime - ve.Start);

        Double newCoordX = originX + (cutoutWidth * cursorValue.X);
        Double newCoordY = originY - (cutoutHeight * cursorValue.Y);
        cursorValue.X = newCoordX / videoWidth;
        cursorValue.Y = 1 - (newCoordY / videoHeight);
        motionParam.SetValueAtTime((cursorTime - ve.Start), cursorValue);

        DialogResult dialogResult = MessageBox.Show("If you choose to also adapt the mask scale, this would mean that the mask will be shrunk together with the video frame.\nIf you have zoomed in alot, it sometimes makes sense to not do this as the control handles would get so small that you can't grab them.\nIf you choose to also adjust the size, you can also later on change the width/height from the mask settings.\n\nWould you like to also adapt the mask scale?", "Also adjust mask scale?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (dialogResult == DialogResult.Yes)
        {
            OFXDoubleParameter widthParam = fx.FindParameterByName("Width_" + mask_to_use.ToString()) as OFXDoubleParameter;
            OFXDoubleParameter heightParam = fx.FindParameterByName("Height_" + mask_to_use.ToString()) as OFXDoubleParameter;
            Double maskWidth = widthParam.GetValueAtTime(cursorTime - ve.Start);
            Double maskHeight = heightParam.GetValueAtTime(cursorTime - ve.Start);
            Double widthRelation = videoWidth / cutoutWidth;
            Double heightRelation = videoHeight / cutoutHeight;

            widthParam.SetValueAtTime((cursorTime - ve.Start), (maskWidth / widthRelation));
            heightParam.SetValueAtTime((cursorTime - ve.Start), (maskHeight / heightRelation));
        }

        VideoMotionBounds restoreBounds = new VideoMotionBounds(
                new VideoMotionVertex(0, 0),
                new VideoMotionVertex(videoWidth, 0),
                new VideoMotionVertex(videoWidth, videoHeight),
                new VideoMotionVertex(0, videoHeight)
                );
        currKeyframe.Bounds = restoreBounds;
        currKeyframe.Center = new VideoMotionVertex((videoWidth / 2), (videoHeight / 2));

        MessageBox.Show("Please select a different effect, or move the cursor to a differen event, in order to update the control handles of the mask", "Refresh control handles", MessageBoxButtons.OK, MessageBoxIcon.Information);

        fx.AllParametersChanged();
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

    private VideoEvent GetEventContainingEffect(VideoEvent[] ve, string effect_name)
    {
        foreach (VideoEvent videoEvent in ve)
        {
            foreach (Effect fx in videoEvent.Effects)
            {
                if (fx.Description.Equals(effect_name))
                {
                    return videoEvent;
                }
            }
        }

        return null;
    }

    private VideoEvent GetEventDifferentFrom(VideoEvent[] ve, VideoEvent comparison)
    {
        foreach (VideoEvent videoEvent in ve)
        {
            if (!videoEvent.Equals(comparison))
            {
                return videoEvent;
            }
        }

        return null;
    }

    private Effect GetEffectFromEvent(VideoEvent ve, string effect_name)
    {
        foreach (Effect fx in ve.Effects)
        {
            if (fx.Description.Equals(effect_name))
            {
                return fx;
            }
        }

        return null;
    }

    private OFXEffect GetOFXFromEvent(VideoEvent ve, string effect_name)
    {
        foreach (Effect fx in ve.Effects)
        {
            if (fx.Description.Equals(effect_name))
            {
                return fx.OFXEffect;
            }
        }

        return null;
    }

    private void PopulateMaskBitmask(OFXEffect fx)
    {
        string[] paramNames = { "Enable_0", "Enable_1", "Enable_2", "Enable_3", "Enable_4" };

        for (int i = 0; i < paramNames.Length; ++i)
        {
            int BITMASK_NUMBER = (int) Math.Pow(2, i);

            OFXBooleanParameter param = fx.FindParameterByName(paramNames[i]) as OFXBooleanParameter;

            if (param.Value)
            {
                MASK_BITMASK += BITMASK_NUMBER;
            }
        }
    }

    private int PromptForMaskNumber()
    {
        Form QuestionForm = new Form();
        QuestionForm.Text = "Mask Selection";
        Label labelMaskChoice = new Label();
        labelMaskChoice.Text = "Choose a Mask to copy from:";
        labelMaskChoice.Location = new Point(1, 1);
        labelMaskChoice.Size = new Size(200, labelMaskChoice.Size.Height);

        ComboBox MaskChoices = new ComboBox();

        MaskChoices.Location = new Point(1, labelMaskChoice.Location.Y + labelMaskChoice.Height + 5);

        string[] mask_choices = { "Mask 1", "Mask 2", "Mask 3", "Mask 4", "Mask 5" };

        for (int i = 0; i < mask_choices.Length; ++i)
        {
            int BITMASK_NUMBER = (int) Math.Pow(2, i);

            if ((MASK_BITMASK & BITMASK_NUMBER) != 0)
            {
                MaskChoices.Items.Add(mask_choices[i]);
            }
        }
        MaskChoices.SelectedIndex = 0;

        Button Done = new Button();
        Done.Click += (s, g) => { Button b = (Button)s; Form f = (Form)b.Parent; f.Close(); };
        MaskChoices.KeyDown += (s, g) => { if (g.KeyCode == Keys.Enter) { ComboBox cb = (ComboBox)s; Form f = (Form)cb.Parent; f.Close(); } };
        Done.Text = "Done";
        Done.Location = new Point(1, MaskChoices.Location.Y + MaskChoices.Height + 5); ;
        QuestionForm.Controls.Add(MaskChoices);
        QuestionForm.Controls.Add(Done);
        QuestionForm.Controls.Add(labelMaskChoice);
        QuestionForm.FormBorderStyle = FormBorderStyle.FixedDialog;
        QuestionForm.AutoSize = true;
        QuestionForm.Height = Done.Location.Y + Done.Height + 5; //This is too small for the form, it autosizes to "big enough"
        QuestionForm.Width = MaskChoices.Location.X + MaskChoices.Width + 5;
        QuestionForm.ShowDialog();

        if (MaskChoices.SelectedIndex >= 0)
        {
            string selectedText = MaskChoices.SelectedItem.ToString();
            char last_char = selectedText[selectedText.Length - 1];
            int selectedMask = int.Parse(last_char.ToString()) - 1;
            int BITMASK_NUMBER = (int) Math.Pow(2, (selectedMask));

            /*
             * MessageBox.Show("Selected Index: " + MaskChoices.SelectedIndex.ToString() + "\n"
             *      + "BITMASK_NUMBER: " + BITMASK_NUMBER.ToString() + "\n"
             *      + "MASK_BITMASK: " + MASK_BITMASK + "\n"
             *      + "MASK_BITMASK & BITMASK_NUMBER: " + (MASK_BITMASK & BITMASK_NUMBER));
             */

            if ((MASK_BITMASK & BITMASK_NUMBER) == 0)
            {
                MessageBox.Show("This Mask is not enabled, or you have selected this Mask twice!\n\nTerminating...", "Wrong Mask", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return -1;
            }

            MASK_BITMASK -= BITMASK_NUMBER;

            return selectedMask;
        }

        return -1;
    }

    private void show_fx_parameters(OFXEffect fx)
    {
        string paramNames = "";
        foreach(OFXParameter param in fx.Parameters)
        {
            paramNames += param.Name + " -> " + param.Label + "\n";
        }
        MessageBox.Show(paramNames);
    }
}
