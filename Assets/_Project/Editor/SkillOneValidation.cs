namespace PersistentWorld.Editor
{
    // Compatibility command; the revised skill is a cone knockback, not two projectiles.
    public static class SkillOneValidation
    {
        public static void RunBatch() => ClassGameplayValidation.RunBatch();
    }
}
