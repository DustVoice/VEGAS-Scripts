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
    const string PIP_FX_NAME = "VEGAS Picture In Picture";
    const string PIP_FX_MODE_NAME = "Free Form";

    const int MASK_0 = 1;
    const int MASK_1 = 2;
    const int MASK_2 = 4;
    const int MASK_3 = 8;
    const int MASK_4 = 16;
    int MASK_BITMASK = 0;
    int CORNER_BITMASK = MASK_0 + MASK_1 + MASK_2 + MASK_3;

    public void FromVegas(Vegas vegas) {
        TrackEvent[] tes = FindSelectedEvents(vegas.Project);
        VideoEvent[] ves = GetVideoEvents(tes);

        if (ves.Length != 2)
        {
            MessageBox.Show("You have to select exactly 2 video events (in no particular order) in order for this script to work.\nOne of the events must contain the \"" + MOTION_TRACKING_FX_NAME + "\" effect with at least one mask enabled and populated with motion tracking data, the second event must contain the \"" + PIP_FX_NAME + "\" effect with the mode set to \"" + PIP_FX_MODE_NAME + "\".\nThen after clicking on this script you can select the mask and corner to use. The script will copy the location values of the mask to the location values of the pip corner (beginning from the cursor position till the end of the motion tracked clip, or within the selection range).\n\nTerminating...", "Not enough selections", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show("No selected event with the \"" + MOTION_TRACKING_FX_NAME + "\" plugin found which holds the motion tracking data!\n\nTerminating...", "Not enough selections", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        VideoEvent ve2 = GetEventContainingEffect(ves, PIP_FX_NAME);

        if (ve2 == null)
        {
            MessageBox.Show("No selected event with the \"" + PIP_FX_NAME + "\" plugin found which is the target for the motion tracking data!\n\nTerminating...", "Not enough selections", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        OFXEffect fx2 = GetOFXFromEvent(ve2, PIP_FX_NAME);

        if (fx2 == null)
        {
            MessageBox.Show("Can't seem to grab the \"" + PIP_FX_NAME + "\" effect!\n\nTerminating...", "Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            return;
        }

        OFXChoiceParameter param = fx2.FindParameterByName("KeepProportions") as OFXChoiceParameter;

        OFXChoice choice = param.Value;

        if (!param.Value.Name.Equals(PIP_FX_MODE_NAME))
        {
            MessageBox.Show("The Mode of the \"" + PIP_FX_NAME + "\" effect has to be set to \"" + PIP_FX_MODE_NAME + "\"!\n\nTerminating...", "Wrong effect mode", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool goOn = false;
        List<int> masks_to_use = new List<int>();
        List<string> corners_to_use = new List<string>();

        while (!goOn)
        {
            int mask_to_use = PromptForMaskNumber();

            if (mask_to_use == -1)
            {
                MessageBox.Show("Something went wrong during the mask choosing process!\n\nTerminating...", "Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            // show_fx_parameters(fx2);

            string corner_to_use = PromptForCorner();

            if (corner_to_use == null)
            {
                MessageBox.Show("Something went wrong during the corner choosing process!\n\nTerminating...", "Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            masks_to_use.Add(mask_to_use);
            corners_to_use.Add(corner_to_use);

            if (MASK_BITMASK == 0 || CORNER_BITMASK == 0)
            {
                goOn = true;
                continue;
            }

            DialogResult continueResult = MessageBox.Show("Do you want to choose another mask-corner pair?", "Another one?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (continueResult == DialogResult.Cancel)
            {
                return;
            }
            else if (continueResult == DialogResult.No )
            {
                goOn = true;
            }
        }

        if (masks_to_use.Count != corners_to_use.Count)
        {
            MessageBox.Show("Something went wrong during the mask-corner choosing process!\nMask-count differs from Corner-count!\n\nMaybe you have selected the same Mask or corner twice?\n\nTerminating...", "Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        }

        Timecode startingTime = null;
        Timecode endingTime = null;

        DialogResult dialogResult = MessageBox.Show("You have two methods to copy the keyframes:\nOne option is to copy from the current cursor position onwards, till the script runs into the ending of one of the events (yes).\nThe other option is to use the current selection. Note however that this produces unexpected behaviour, if no selection is currently active! (no).\n\nCopy from the cursor onwards?", "Processing Method", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        if (dialogResult == DialogResult.Cancel)
        {
            return;
        }
        else
        {
            Double motionStart = ve.Start.ToMilliseconds();
            Double motionEnd = ve.End.ToMilliseconds();

            Double cornerStart = ve2.Start.ToMilliseconds();
            Double cornerEnd = ve2.End.ToMilliseconds();

            if (dialogResult == DialogResult.Yes)
            {
                Double cursorStart = vegas.Transport.CursorPosition.ToMilliseconds();

                Double max = Math.Max(motionStart, Math.Max(cornerStart, cursorStart));
                Double min = Math.Min(motionEnd, Math.Min(cornerEnd, cursorStart));

                if (max != cursorStart || min != cursorStart)
                {
                    MessageBox.Show("The cursor must be placed at a position where it covers both selected events!\n\nTerminating...", "Invalid cursor position", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                startingTime = new Timecode(cursorStart);
                endingTime = new Timecode(Math.Min(motionEnd, cornerEnd));
            }
            else if (dialogResult == DialogResult.No)
            {
                Double selectionStart = vegas.Transport.SelectionStart.ToMilliseconds();
                Double selectionEnd = selectionStart + vegas.Transport.SelectionLength.ToMilliseconds();

                Double max = Math.Max(motionStart, Math.Max(cornerStart, selectionStart));
                Double min = Math.Min(motionEnd, Math.Min(cornerEnd, selectionEnd));

                if (max != selectionStart || min != selectionEnd)
                {
                    MessageBox.Show("The selection must be placed in a range where it covers both selected events!\n\nTerminating...", "Invalid selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                startingTime = new Timecode(selectionStart);
                endingTime = new Timecode(selectionEnd);
            }
        }

        // MessageBox.Show("Current time: " + fx2.CurrentTime.ToString() + "\nCursor pos: " + vegas.Transport.CursorPosition.ToString() + "\nCalculated current time: " + (fx2.CurrentTime + ve2.Start).ToString());

        if (startingTime == null || endingTime == null)
        {
            MessageBox.Show("Something went wrong as the script tried to use the method you decided...\n\nTerminating...", "Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            return;
        }

        PopulateMaskBitmask(fx);
        CORNER_BITMASK = MASK_0 + MASK_1 + MASK_2 + MASK_3;

        for (int i = 0; i < masks_to_use.Count; ++i)
        {
            OFXDouble2DParameter motionParam = fx.FindParameterByName("Location_" + masks_to_use[i].ToString()) as OFXDouble2DParameter;

            OFXDouble2DParameter cornerParam = fx2.FindParameterByName(corners_to_use[i]) as OFXDouble2DParameter;

            foreach (OFXDouble2DKeyframe motionKeyframe in motionParam.Keyframes)
            {
                Timecode keyframeTime = motionKeyframe.Time + ve.Start;
                if (startingTime <= keyframeTime && keyframeTime <= endingTime)
                {
                    cornerParam.SetValueAtTime((keyframeTime - ve2.Start), motionKeyframe.Value);
                }
            }

            cornerParam.ParameterChanged();
        }

        // dialogResult = MessageBox.Show("Do you also want to copy the interpolation data?", "Copy interpolation data?", MessageBoxButtons.YesNo);

        // if (dialogResult == DialogResult.Yes)
        // {
        //     int curr = 0;
        //     int end = cornerParam.Keyframes.Count;

        //     foreach (OFXDouble2DKeyframe motionKeyframe in motionParam.Keyframes)
        //     {
        //         Timecode keyframeTime = motionKeyframe.Time + ve.Start;
        //         if (curr < end && startingTime <= keyframeTime && keyframeTime <= endingTime)
        //         {
        //             Timecode calculatedTime = (keyframeTime - ve2.Start);

        //             OFXDouble2DKeyframe cornerKeyframe = cornerParam.Keyframes[curr] as OFXDouble2DKeyframe;
        //             if (cornerKeyframe.Time == calculatedTime)
        //             {
        //                 cornerKeyframe.Interpolation = motionKeyframe.Interpolation;
        //             }
        //             else if (cornerKeyframe.Time < calculatedTime)
        //             {
        //                 ++curr;
        //             }
        //         }
        //     }
        // }

        // cornerParam.ParameterChanged();
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

    private string PromptForCorner()
    {
        Form QuestionForm = new Form();
        QuestionForm.Text = "Corner Selection";
        Label labelCornerChoice = new Label();
        labelCornerChoice.Text = "Choose a corner to copy to:";
        labelCornerChoice.Location = new Point(1, 1);
        labelCornerChoice.Size = new Size(200, labelCornerChoice.Size.Height);

        ComboBox CornerChoices = new ComboBox();

        CornerChoices.Location = new Point(1, labelCornerChoice.Location.Y + labelCornerChoice.Height + 5);

        string[] corner_choices = { "Top Left", "Top Right", "Bottom Left", "Bottom Right" };

        for (int i = 0; i < corner_choices.Length; ++i)
        {
            int BITMASK_NUMBER = (int) Math.Pow(2, i);

            if ((CORNER_BITMASK & BITMASK_NUMBER) != 0)
            {
                CornerChoices.Items.Add(corner_choices[i]);
            }
        }
        CornerChoices.SelectedIndex = 0;

        Button Done = new Button();
        Done.Click += (s, g) => { Button b = (Button)s; Form f = (Form)b.Parent; f.Close(); };
        CornerChoices.KeyDown += (s, g) => { if (g.KeyCode == Keys.Enter) { ComboBox cb = (ComboBox)s; Form f = (Form)cb.Parent; f.Close(); } };
        Done.Text = "Done";
        Done.Location = new Point(1, CornerChoices.Location.Y + CornerChoices.Height + 5); ;
        QuestionForm.Controls.Add(CornerChoices);
        QuestionForm.Controls.Add(Done);
        QuestionForm.Controls.Add(labelCornerChoice);
        QuestionForm.FormBorderStyle = FormBorderStyle.FixedDialog;
        QuestionForm.AutoSize = true;
        QuestionForm.Height = Done.Location.Y + Done.Height + 5; //This is too small for the form, it autosizes to "big enough"
        QuestionForm.Width = CornerChoices.Location.X + CornerChoices.Width + 5;
        QuestionForm.ShowDialog();

        if (CornerChoices.SelectedIndex >= 0)
        {
            string selectedText = CornerChoices.SelectedItem.ToString();

            int selectedCorner = -1;

            switch (selectedText)
            {
                case "Top Left":
                    selectedCorner = 0;
                    break;
                case "Top Right":
                    selectedCorner = 1;
                    break;
                case "Bottom Left":
                    selectedCorner = 2;
                    break;
                case "Bottom Right":
                    selectedCorner = 3;
                    break;
                default:
                    break;
            }

            if (selectedCorner == -1)
            {
                MessageBox.Show("Couldn't grab ComboBox content. This could be a bug. Please try again!\n\nTerminating...", "Wrong Corner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            int BITMASK_NUMBER = (int) Math.Pow(2, (selectedCorner));

            if ((CORNER_BITMASK & BITMASK_NUMBER) == 0)
            {
                MessageBox.Show("This corner has already be chosen!\n\nTerminating...", "Wrong Corner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            CORNER_BITMASK -= BITMASK_NUMBER;

            switch (selectedCorner)
            {
                case 0:
                    return "CornerTL";
                case 1:
                    return "CornerTR";
                case 2:
                    return "CornerBL";
                case 3:
                    return "CornerBR";
                default:
                    return null;
            }
        }

        return null;
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
