using UnityEngine;

namespace OfficeImposter
{
    public static class CursorLock
    {
        public static bool IsLocked { get; private set; }

        public static void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            IsLocked = true;
        }

        public static void Unlock()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsLocked = false;
        }

        public static void Toggle()
        {
            if (IsLocked) Unlock();
            else Lock();
        }
    }
}
