I want to use Spectre console to select from a list of DICOM files from a patient tree. The tree top-level items are courses and series.

Below courses, courses by the way are not DICOM objects but all of its children are, the first item under a course is a plan. A plan has three children itself, a structure set, an image, and a dose file. So there are two layers to that to the course tree: course, plan, and then below that is a single set of children: dose, image, and structure set. Then in the same level as the courses are series and below series are just instances. 

I don't believe Spectre console has any more than a single depth tree selection using the multi-selector or multi-selection prompt. So I think what we need to do is do two stages of selection:
1. For courses and series
2. Once they choose either a course or series, that will open up and then show the next layer which will show the plans if it's course that's selected, and then the next layer after that will be structure sets, doses, and images.
If a series is selected, then only a single layer of instances is below that. So the series tree is actually a lot simpler than the course tree which has multiple layers.

An example multiselction prompt is:

List<string> courseIds = new List<string>(){"C1","C2","C3"};
List<string> seriesIds = new List<string>(){"Series1", "Series2", "Series3"};

List<string> dicomItems = AnsiConsole.Prompt(
    new MultiSelectionPrompt<string>()
    .Title("Which items do you want to export?")
    .InstructionsText("Press <space> to toggle, <enter> to accept")
    //.AddChoices(usualNames)
    .AddChoiceGroup("Courses", courseIds)
    .AddChoiceGroup("Series", seriesIds)
);

A single course can have a few plans. The tree can look like this:
C1
-Plan 1
--Dose (Plan 1)
--Structure Set (Plan 1)
--Image (Plan 1)
-Plan 2
--Dose (Plan 2)
--Structure Set (Plan 2)
--Image (Plan 2)

And series looks like this
Series 1 (Plans)
-Plan 1
-Plan 2
Series 2 (Structure Sets)
-Structure Set 1
-Structure Set 2
etc.

The end goal of the selection process is to find the series and instance objects that need to be exported via automated DICOM export. For series, we will need the Series UID as an identifier to export the series, and for instances, we will need both the Series UID and the Instance UID. All of these will be required to build these trees. 

We will be using the Eclipse Scripting API to find the patient object and walk the Courses and Series to build this tree list for users to see. 

    public class TreeBuilder
    {
        public DicomTree BuildTree(VMS.TPS.Common.Model.API.Application app, string patientId)
        {
             var pat = app.OpenPatientById(patientId);
             var courses = pat.Courses.Select(c=> c.Id).ToList();
             foreach (var course in courses)
             {
                var courseObj = pat.Courses.FirstOrDefault(c => c.Id == course);
                var plans = courseObj.PlanSetups.Select(p => p.Id).ToList();
                foreach (var plan in plans)
                {
                    var planObj = courseObj.PlanSetups.FirstOrDefault(tp => tp.Id == plan);
                    var planUid = planObj.UID;
                    var planSeriesUid = planObj.SeriesUID;
                    var structureset = planObj.StructureSet;
                    var structureSetUid = structureset.UID;
                    var structureSetSeriesUid = structureset.SeriesUID;
                    var image = planObj.StructureSet.Image;
                    var imageSeriesUid = image.Series.UID; 
                    var dose = planObj.Dose;
                    var doseUid = dose.UID;
                    var doseSeriesUid = dose.SeriesUID;
                }
            }

             foreach(var series in pat.Studies.SelectMany(s => s.Series))
             {
                var seriesUid = series.UID;
                var seriesModality = series.Modality;
                //Need a friendly name if available:

            try
            {
                switch (series.Modality)
                {
                    case SeriesModality.CT:
                    case SeriesModality.MR:
                    case SeriesModality.PT:
                    case SeriesModality.RTIMAGE:
                        var friendly = string.Empty;
                        if (series.Images.Any())
                        {
                            foreach (var im in series.Images)
                            {
                                try
                                {
                                    if (!double.IsNaN(im.Origin.x)) { friendly = im.Id; break; }
                                }
                                catch (Exception e)
                                {

                                }
                            }
                        }
                        return friendly;
                    case SeriesModality.RTSTRUCT:
                        var match = pat.StructureSets.FirstOrDefault(st => st.Image.HistoryDateTime == series.HistoryDateTime);
                        if (match != null)
                        {
                            return match.Id;
                        }
                        return string.Empty;
                    default: return string.Empty;
                }
            }
            catch (Exception e)
            {
                return null;
            }

        }
    }

