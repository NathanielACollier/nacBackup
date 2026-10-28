using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using nacBackupWPF.models;

namespace nacBackupWPF.repos;

/**
 * <summary>
 *  Result of a confirmation prompt to the user.
 * </summary>
 */
public class PromptResult
{
    public bool Confirmed { get; set; }
}
