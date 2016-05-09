using Microsoft.Build.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.VisualStudio
{
    
    internal class ProjectWithInterfaceDefinitions : Project //, IDisposable
    {
        public const string InterfaceDefinitionItemType = "InterfaceDefinition";

        /*        [CompilerGenerated]
                [Serializable]
                private sealed class __c
                {
                    public static readonly ComponentsProject.__c c__9 = new ComponentsProject.__c();

                    public static Func<ProjectItem, bool> c__9__3_0;

                    public static Func<ProjectMetadata, bool> c__9__3_1;

                    public static Func<ProjectMetadata, bool> c__9__3_2;

                    public static Func<ProjectMetadata, bool> c__9__5_1;

                    public static Func<ProjectMetadata, bool> c__9__5_2;

                    public static Func<ProjectMetadata, bool> c__9__6_1;

                    internal bool <GetComponentReferences>b__3_0(ProjectItem item)
                    {
                        return "XamarinComponentReference".Equals(item.ItemType, StringComparison.OrdinalIgnoreCase);
                    }

                    internal bool <GetComponentReferences>b__3_1(ProjectMetadata metadata)
                    {
                        return metadata.Name == "Version";
                    }

                    internal bool <GetComponentReferences>b__3_2(ProjectMetadata metadata)
                    {
                        return metadata.Name == "InstallationInProgress";
                    }

                    internal bool <UpdateItem>b__5_1(ProjectMetadata metadata)
                    {
                        return metadata.Name == "Version";
                    }

                    internal bool <UpdateItem>b__5_2(ProjectMetadata metadata)
                    {
                        return metadata.Name == "InstallationInProgress";
                    }

                    internal bool <RemoveItem>b__6_1(ProjectMetadata metadata)
                    {
                        return metadata.Name == "Version";
                    }
                }*/
        /*

        internal InterfaceDefinitionsProject(string projectFilename) : base(projectFilename)
        {
        }

        public void Dispose()
        {
            base.ProjectCollection.UnloadProject(this);
        }

        [IteratorStateMachine(typeof(ComponentsProject.< GetComponentReferences > d__3))]
        internal IEnumerable<ComponentReference> GetComponentReferences()
        {
            Microsoft.VisualStudio.Shell.Interop.IVsSolution s;
            Microsoft.VisualStudio.Shell.Interop.IVsHierarchy h;
            s.GetProjectOfGuid(Guid.Empty, out h);
            h.ToDteProject().ToVsProject()..ProjectItems.Item(0).

            Items.Where(item => item.ItemType == InterfaceDefinitionItemType);


            IEnumerable<ProjectItem> arg_49_0 = this.Items;
            Func<ProjectItem, bool> arg_49_1;
            if ((arg_49_1 = ComponentsProject.__c.c__9__3_0) == null) {
                arg_49_1 = (ComponentsProject.__c.c__9__3_0 = new Func<ProjectItem, bool>(ComponentsProject.__c.c__9.< GetComponentReferences > b__3_0));
            }
            IEnumerable<ProjectItem> enumerable = arg_49_0.Where(arg_49_1);
            foreach (ProjectItem current in enumerable) {
                if (!string.IsNullOrEmpty(current.EvaluatedInclude)) {
                    IEnumerable<ProjectMetadata> arg_A9_0 = current.Metadata;
                    Func<ProjectMetadata, bool> arg_A9_1;
                    if ((arg_A9_1 = ComponentsProject.__c.c__9__3_1) == null) {
                        arg_A9_1 = (ComponentsProject.__c.c__9__3_1 = new Func<ProjectMetadata, bool>(ComponentsProject.__c.c__9.< GetComponentReferences > b__3_1));
                    }
                    ProjectMetadata projectMetadata = arg_A9_0.FirstOrDefault(arg_A9_1);
                    Version version;
                    if (projectMetadata != null && Version.TryParse(projectMetadata.UnevaluatedValue, out version)) {
                        IEnumerable<ProjectMetadata> arg_E9_0 = current.Metadata;
                        Func<ProjectMetadata, bool> arg_E9_1;
                        if ((arg_E9_1 = ComponentsProject.__c.c__9__3_2) == null) {
                            arg_E9_1 = (ComponentsProject.__c.c__9__3_2 = new Func<ProjectMetadata, bool>(ComponentsProject.__c.c__9.< GetComponentReferences > b__3_2));
                        }
                        ProjectMetadata projectMetadata2 = arg_E9_0.FirstOrDefault(arg_E9_1);
                        yield return new ComponentReference(current.EvaluatedInclude, version, projectMetadata2 != null && "true".Equals(projectMetadata2.UnevaluatedValue, StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
            IEnumerator<ProjectItem> enumerator = null;
            yield break;
            yield break;
        }

        internal void AddItem(ComponentReference reference)
        {
            base.MarkDirty();
            ProjectItem projectItem = base.AddItem("XamarinComponentReference", reference.Id).FirstOrDefault<ProjectItem>();
            if (projectItem != null) {
                projectItem.SetMetadataValue("Visible", "False");
                projectItem.SetMetadataValue("Version", reference.Version.ToString());
                projectItem.SetMetadataValue("InstallationInProgress", reference.InstallationInProgress.ToString());
            }
        }

        internal void UpdateItem(ComponentReference reference)
        {
            foreach (ProjectItem current in (from item in base.Items
                                             where "XamarinComponentReference".Equals(item.ItemType, StringComparison.OrdinalIgnoreCase) && string.Equals(reference.Id, item.EvaluatedInclude, StringComparison.OrdinalIgnoreCase)
                                             select item).ToList<ProjectItem>()) {
                if (!string.IsNullOrEmpty(current.EvaluatedInclude)) {
                    IEnumerable<ProjectMetadata> arg_71_0 = current.Metadata;
                    Func<ProjectMetadata, bool> arg_71_1;
                    if ((arg_71_1 = ComponentsProject.__c.c__9__5_1) == null) {
                        arg_71_1 = (ComponentsProject.__c.c__9__5_1 = new Func<ProjectMetadata, bool>(ComponentsProject.__c.c__9.< UpdateItem > b__5_1));
                    }
                    ProjectMetadata projectMetadata = arg_71_0.FirstOrDefault(arg_71_1);
                    if (projectMetadata != null) {
                        base.MarkDirty();
                        projectMetadata.UnevaluatedValue = reference.Version.ToString();
                    }
                    IEnumerable<ProjectMetadata> arg_BB_0 = current.Metadata;
                    Func<ProjectMetadata, bool> arg_BB_1;
                    if ((arg_BB_1 = ComponentsProject.__c.c__9__5_2) == null) {
                        arg_BB_1 = (ComponentsProject.__c.c__9__5_2 = new Func<ProjectMetadata, bool>(ComponentsProject.__c.c__9.< UpdateItem > b__5_2));
                    }
                    ProjectMetadata projectMetadata2 = arg_BB_0.FirstOrDefault(arg_BB_1);
                    if (projectMetadata2 != null && !reference.InstallationInProgress) {
                        base.MarkDirty();
                        current.RemoveMetadata(projectMetadata2.Name);
                    }
                }
            }
        }

        internal void RemoveItem(ComponentReference reference)
        {
            foreach (ProjectItem current in (from item in base.Items
                                             where "XamarinComponentReference".Equals(item.ItemType, StringComparison.OrdinalIgnoreCase) && string.Equals(reference.Id, item.EvaluatedInclude, StringComparison.OrdinalIgnoreCase)
                                             select item).ToList<ProjectItem>()) {
                if (!string.IsNullOrEmpty(current.EvaluatedInclude)) {
                    IEnumerable<ProjectMetadata> arg_6B_0 = current.Metadata;
                    Func<ProjectMetadata, bool> arg_6B_1;
                    if ((arg_6B_1 = ComponentsProject.__c.c__9__6_1) == null) {
                        arg_6B_1 = (ComponentsProject.__c.c__9__6_1 = new Func<ProjectMetadata, bool>(ComponentsProject.__c.c__9.< RemoveItem > b__6_1));
                    }
                    ProjectMetadata projectMetadata = arg_6B_0.FirstOrDefault(arg_6B_1);
                    Version v;
                    if (projectMetadata != null && Version.TryParse(projectMetadata.UnevaluatedValue, out v) && v == reference.Version) {
                        base.MarkDirty();
                        base.RemoveItem(current);
                    }
                }
            }
        }*/
    }
}
