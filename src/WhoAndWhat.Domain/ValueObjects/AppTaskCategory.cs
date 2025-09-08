using WhoAndWhat.Domain.Common;

namespace WhoAndWhat.Domain.ValueObjects;

/// <summary>
/// Rich value object representing AppTask category with business rules and validation
/// </summary>
public record AppAppTaskCategory
{
    public static readonly AppAppTaskCategory ToDo = new("ToDo", 0, "General to-do items", false, false, "#007bff", "task");
    public static readonly AppAppTaskCategory Idea = new("Idea", 1, "Ideas and inspiration", false, true, "#ffc107", "lightbulb");
    public static readonly AppAppTaskCategory Appointment = new("Appointment", 2, "Scheduled appointments", true, false, "#dc3545", "calendar");
    public static readonly AppAppTaskCategory BillReminder = new("BillReminder", 3, "Bill payment reminders", true, false, "#fd7e14", "credit-card");
    public static readonly AppAppTaskCategory Project = new("Project", 4, "Complex projects with subtasks", false, true, "#6f42c1", "folder");

    private static readonly IReadOnlyList<AppAppTaskCategory> AllCategories = new List<AppAppTaskCategory>
    {
        ToDo, Idea, Appointment, BillReminder, Project
    };

    private static readonly IReadOnlyDictionary<int, AppAppTaskCategory> CategoryByValue = AllCategories.ToDictionary(c => c.Value);
    private static readonly IReadOnlyDictionary<string, AppAppTaskCategory> CategoryByName = AllCategories.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

    public string Name { get; }
    public int Value { get; }
    public string Description { get; }
    public bool RequiresDueDate { get; }
    public bool AllowsSubtasks { get; }
    public string ColorCode { get; }
    public string IconName { get; }

    private AppAppTaskCategory(string name, int value, string description, bool requiresDueDate, bool allowsSubtasks, string colorCode, string iconName)
    {
        Name = name;
        Value = value;
        Description = description;
        RequiresDueDate = requiresDueDate;
        AllowsSubtasks = allowsSubtasks;
        ColorCode = colorCode;
        IconName = iconName;
    }

    /// <summary>
    /// Gets all available task categories
    /// </summary>
    /// <returns>Collection of all task categories</returns>
    public static IEnumerable<AppAppTaskCategory> GetAll() => AllCategories;

    /// <summary>
    /// Creates AppAppTaskCategory from integer value
    /// </summary>
    /// <param name="value">Integer value</param>
    /// <returns>AppAppTaskCategory instance</returns>
    /// <exception cref="ArgumentException">When value is invalid</exception>
    public static AppAppTaskCategory FromValue(int value)
    {
        if (CategoryByValue.TryGetValue(value, out var category))
        {
            return category;
        }

        throw new ArgumentException($"Invalid task category value: {value}", nameof(value));
    }

    /// <summary>
    /// Creates AppTaskCategory from name string
    /// </summary>
    /// <param name="name">Category name</param>
    /// <returns>AppAppTaskCategory instance</returns>
    /// <exception cref="ArgumentException">When name is invalid</exception>
    public static AppTaskCategory FromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task category name cannot be null or empty", nameof(name));
        }

        if (CategoryByName.TryGetValue(name.Trim(), out var category))
        {
            return category;
        }

        throw new ArgumentException($"Invalid task category name: {name}", nameof(name));
    }

    /// <summary>
    /// Tries to create AppTaskCategory from integer value
    /// </summary>
    /// <param name="value">Integer value</param>
    /// <param name="category">Output AppTaskCategory if successful</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool TryFromValue(int value, out AppTaskCategory? category)
    {
        return CategoryByValue.TryGetValue(value, out category);
    }

    /// <summary>
    /// Tries to create AppTaskCategory from name string
    /// </summary>
    /// <param name="name">Category name</param>
    /// <param name="category">Output AppTaskCategory if successful</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool TryFromName(string name, out AppTaskCategory? category)
    {
        category = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return CategoryByName.TryGetValue(name.Trim(), out category);
    }

    /// <summary>
    /// Gets categories that can be converted to projects
    /// </summary>
    /// <returns>Collection of convertible categories</returns>
    public static IEnumerable<AppTaskCategory> GetProjectConvertibleCategories()
    {
        return AllCategories.Where(c => c.AllowsSubtasks || c == Idea);
    }

    /// <summary>
    /// Gets categories that are time-sensitive (require due dates)
    /// </summary>
    /// <returns>Collection of time-sensitive categories</returns>
    public static IEnumerable<AppTaskCategory> GetTimeSensitiveCategories()
    {
        return AllCategories.Where(c => c.RequiresDueDate);
    }

    /// <summary>
    /// Determines if this category can be converted to the target category
    /// </summary>
    /// <param name="targetCategory">Target category</param>
    /// <returns>True if conversion is allowed</returns>
    public bool CanConvertTo(AppTaskCategory targetCategory)
    {
        // Cannot convert to the same category
        if (this == targetCategory)
        {
            return false;
        }

        // Business rules for category conversion
        return this switch
        {
            _ when this == Idea => true, // Ideas can become anything
            _ when this == ToDo => targetCategory != Idea, // ToDos cannot become ideas
            _ when this == Appointment => targetCategory == ToDo, // Appointments can only become general tasks
            _ when this == BillReminder => targetCategory == ToDo, // Bill reminders can only become general tasks
            _ when this == Project => false, // Projects cannot be converted to other categories
            _ => false
        };
    }

    /// <summary>
    /// Gets valid conversion targets for this category
    /// </summary>
    /// <returns>Collection of valid target categories</returns>
    public IEnumerable<AppTaskCategory> GetValidConversions()
    {
        return AllCategories.Where(CanConvertTo);
    }

    /// <summary>
    /// Validates task data against category requirements
    /// </summary>
    /// <param name="title">Task title</param>
    /// <param name="description">Task description</param>
    /// <param name="dueDate">Task due date</param>
    /// <param name="hasSubtasks">Whether task has subtasks</param>
    /// <returns>Validation result</returns>
    public ValidationResult ValidateTaskData(string? title, string? description, DateTime? dueDate, bool hasSubtasks = false)
    {
        var errors = new List<string>();

        // Title validation
        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add("Title is required for all task categories");
        }

        // Due date validation
        if (RequiresDueDate && !dueDate.HasValue)
        {
            errors.Add($"{GetDisplayName()} tasks must have a due date");
        }

        // Subtasks validation
        if (hasSubtasks && !AllowsSubtasks)
        {
            errors.Add($"{GetDisplayName()} tasks cannot have subtasks");
        }

        // Category-specific validation
        switch (this.Name)
        {
            case "Appointment":
                if (dueDate.HasValue && dueDate.Value < DateTime.UtcNow)
                {
                    errors.Add("Appointment date cannot be in the past");
                }
                break;

            case "BillReminder":
                if (string.IsNullOrWhiteSpace(description))
                {
                    errors.Add("Bill reminders should include payment details in the description");
                }
                break;

            case "Project":
                if (string.IsNullOrWhiteSpace(description))
                {
                    errors.Add("Projects should include a detailed description");
                }
                break;

            case "Idea":
                // Ideas are flexible - no strict requirements
                break;

            case "ToDo":
                // Basic to-dos have minimal requirements
                break;
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }

    /// <summary>
    /// Gets the display-friendly name of the category
    /// </summary>
    /// <returns>Display name</returns>
    public string GetDisplayName() => Name switch
    {
        "ToDo" => "To-Do",
        "Idea" => "Idea",
        "Appointment" => "Appointment",
        "BillReminder" => "Bill Reminder",
        "Project" => "Project",
        _ => Name
    };

    /// <summary>
    /// Gets CSS class name for UI styling
    /// </summary>
    /// <returns>CSS class name</returns>
    public string GetCssClass() => Name.ToLowerInvariant() switch
    {
        "todo" => "category-todo",
        "idea" => "category-idea",
        "appointment" => "category-appointment",
        "billreminder" => "category-bill-reminder",
        "project" => "category-project",
        _ => "category-unknown"
    };

    /// <summary>
    /// Gets priority suggestions for this category
    /// </summary>
    /// <returns>Suggested priority levels</returns>
    public IEnumerable<Priority> GetSuggestedPriorities() => Name switch
    {
        "Appointment" => new[] { Priority.High, Priority.Urgent },
        "BillReminder" => new[] { Priority.High, Priority.Medium },
        "Project" => new[] { Priority.Medium, Priority.High },
        "ToDo" => Priority.GetAll(),
        "Idea" => new[] { Priority.Low, Priority.Medium },
        _ => Priority.GetAll()
    };

    /// <summary>
    /// Gets estimated completion time in hours for planning
    /// </summary>
    /// <returns>Estimated hours</returns>
    public double GetEstimatedHours() => Name switch
    {
        "ToDo" => 1.0,
        "Idea" => 0.5,
        "Appointment" => 2.0,
        "BillReminder" => 0.25,
        "Project" => 8.0,
        _ => 1.0
    };

    /// <summary>
    /// Determines if tasks in this category should auto-archive after completion
    /// </summary>
    /// <returns>True if auto-archive is recommended</returns>
    public bool ShouldAutoArchive() => Name switch
    {
        "BillReminder" => true,  // Bills paid should be archived quickly
        "Appointment" => true,   // Past appointments should be archived
        "ToDo" => false,         // Keep completed to-dos visible
        "Idea" => false,         // Keep completed ideas for reference
        "Project" => false,      // Keep completed projects visible
        _ => false
    };

    // Implicit conversion from AppTaskCategory to int for database storage
    public static implicit operator int(AppTaskCategory category) => category.Value;

    // Explicit conversion from int to AppTaskCategory
    public static explicit operator AppTaskCategory(int value) => FromValue(value);

    public override string ToString() => Name;
}