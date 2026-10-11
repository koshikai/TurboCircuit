using System.Collections.Generic;
using UnityEngine;

// コース装飾の手動調整データ（DecorEntry/DecorClone は CourseDecor.cs）。
public class CourseDecorData : ScriptableObject
{
    public string courseName;
    public List<DecorEntry> entries = new List<DecorEntry>();
    public List<DecorClone> clones = new List<DecorClone>();

    public static string ResourcePath(int courseIndex) => $"Data/CourseDecor/Course_{courseIndex:D2}";
}
