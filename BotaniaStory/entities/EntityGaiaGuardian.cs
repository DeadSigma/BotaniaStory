using System;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using BotaniaStory.entities.ai;

namespace BotaniaStory.entities
{
    public class EntityGaiaGuardian : EntityHumanoid
    {
        // Радиус арены от точки спавна 
        public const float ArenaRadius = 12f;

        public const float HealthPerExtraPlayer = 1.0f;   // +100% HP за каждого доп. игрока 
        public const float DamagePerExtraPlayer = 0.35f;  // +35% урона за каждого доп. игрока
        public const float LootSetsPerExtraPlayer = 1.0f; // +1 полный набор дропа за каждого доп. игрока
        public const float MaxDamagePerHit = 12f;         // кап урона за один удар: защита от ваншота (0 = без капа)
        public const float BirthDurationSeconds = 6f;     // фаза рождения: бессмертна, не атакует, копит силу

        private const float PlayerHardClampBuffer = 1.0f;   // с какого выхода за кромку жёстко возвращать
        private const float PlayerConfineMargin = 5.0f;     // дальше этого игрока не трогает
        private const float PlayerClampReturnDepth = 0.5f;  // насколько внутрь от кромки ставить игрока

        private ILoadedSound bossMusic;
        private bool isMusicStarted = false;
        // Ритуал сбрасывается, если в арене нет живых игроков дольше этого времени (смерть/уход)
        private const float RitualAbandonSeconds = 1f;

        private float minionScanTimer = 0f;
        private bool confineErrorLogged = false;
        private bool aiSuppressErrorLogged = false;
        private float noPlayerTimer = 0f;
        private bool arenaSupportErrorLogged = false;
        private const float RageEnterDestroyedPercent = 0.40f;
        private const float RageExitDestroyedPercent = 0.35f;

        private const float RageArenaScanInterval = 1.0f;

        private float rageArenaScanTimer = 0f;
        private bool rageScanErrorLogged = false;

        private int PlayerCount => Math.Max(1, WatchedAttributes.GetInt("gaiaPlayerCount", 1));

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            if (api.Side == EnumAppSide.Server)
            {
                WatchedAttributes.SetFloat("fallDamageMultiplier", 0f);
                RemoveDespawnBehavior();

                var tm = GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
                if (tm != null)
                {
                    tm.OnShouldExecuteTask += task => WatchedAttributes.GetFloat("gaiaBirthTimer", 0f) <= 0f;
                }
            }
            else if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;

                // Загружаем как музыку, но без сложной регистрации
                bossMusic = capi.World.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("botaniastory", "sounds/gaia_music"),
                    ShouldLoop = true,
                    DisposeOnFinish = false,
                    Volume = 1.0f,
                    SoundType = EnumSoundType.Music // Трек реагирует на ползунок "Музыка" в настройках!
                });
            }
        }
        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            if (World.Side == EnumAppSide.Client && bossMusic != null)
            {
                bossMusic.Stop();
                bossMusic.Dispose();
                bossMusic = null;
            }
        }

        private void RemoveDespawnBehavior()
        {
            var behaviors = SidedProperties?.Behaviors;
            if (behaviors == null) return;
            for (int i = behaviors.Count - 1; i >= 0; i--)
            {
                if (behaviors[i].PropertyName() == "despawn")
                    behaviors.RemoveAt(i);
            }
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
            if (World.Side == EnumAppSide.Server)
            {
                SaveSpawnCenter();

                // Фаза рождения.
                WatchedAttributes.SetFloat(
                    "gaiaBirthTimer",
                    BirthDurationSeconds
                );

                Controls.IsFlying = true;

                Pos.Motion.X = 0;
                Pos.Motion.Y = 0;
                Pos.Motion.Z = 0;

                // проверяем разрушенность арены сразу при спавне.
                InitializeRageModeOnSpawn();

                ApplyHealthScaling();
                ApplyDamageScaling();
            }
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            if (World.Side == EnumAppSide.Server)
            {
                SaveSpawnCenter();
                ApplyDamageScaling(); 
            }
        }

        private void SaveSpawnCenter()
        {
            if (!WatchedAttributes.HasAttribute("gaiaSpawnPosX"))
            {
                WatchedAttributes.SetDouble("gaiaSpawnPosX", Pos.X);
                WatchedAttributes.SetDouble("gaiaSpawnPosY", Pos.Y);
                WatchedAttributes.SetDouble("gaiaSpawnPosZ", Pos.Z);
            }
        }

        // Бессмертие и ка урона
        // ReceiveDamage - входная точка всего урона. В 1.22 ShouldReceiveDamage принимает damage по значению (без ref), там величину не порезать - клампим здесь, до раздачи behavior'ам
        public override bool ReceiveDamage(DamageSource damageSource, float damage)
        {
            if (World.Side == EnumAppSide.Server && damageSource?.Type != EnumDamageType.Heal)
            {
                // Бессмертна: пока рождается и пока левитирует (спавн волн мобов)
                if (WatchedAttributes.GetFloat("gaiaBirthTimer", 0f) > 0f) return false;
                if (WatchedAttributes.GetBool("isLevitating", false)) return false;

                // Обычная фаза: кап урона за удар (анти-ваншот; обычное оружие проходит целиком)
                if (MaxDamagePerHit > 0f && damage > MaxDamagePerHit) damage = MaxDamagePerHit;
            }

            return base.ReceiveDamage(damageSource, damage);
        }

        // Масштабирование

        private void ApplyHealthScaling()
        {
            if (WatchedAttributes.GetBool("gaiaHpScaled", false)) return;

            float mul = 1f + (PlayerCount - 1) * HealthPerExtraPlayer;
            ITreeAttribute ht = WatchedAttributes.GetTreeAttribute("health");
            if (ht == null) return;

            float baseMax = ht.GetFloat("basemaxhealth", ht.GetFloat("maxhealth", 10f));
            float scaled = baseMax * mul;

            ht.SetFloat("basemaxhealth", scaled);
            ht.SetFloat("maxhealth", scaled);
            ht.SetFloat("currenthealth", scaled);
            WatchedAttributes.SetBool("gaiaHpScaled", true);
            WatchedAttributes.MarkPathDirty("health");
        }

        private void ApplyDamageScaling()
        {
            float mul = 1f + (PlayerCount - 1) * DamagePerExtraPlayer;
            if (mul <= 1f) return;

            try
            {
                var taskAi = GetBehavior<EntityBehaviorTaskAI>();
                var tasks = taskAi?.TaskManager?.AllTasks;
                if (tasks == null) return;

                foreach (var task in tasks)
                {
                    FieldInfo fi = FindFloatField(task.GetType(), "damage");
                    if (fi == null) continue;

                    float baseDmg = (float)fi.GetValue(task);
                    fi.SetValue(task, baseDmg * mul);
                }
            }
            catch (Exception e)
            {
                World.Logger.Warning("[BotaniaStory] Gaia damage scaling failed: {0}", e);
            }
        }

        private static FieldInfo FindFloatField(Type t, string name)
        {
            while (t != null)
            {
                FieldInfo fi = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (fi != null && fi.FieldType == typeof(float)) return fi;
                t = t.BaseType;
            }
            return null;
        }

        // Лут: при честной смерти доспавниваем (игроков-1)*LootSetsPerExtraPlayer дополнительных наборов дропа
        public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null)
        {
            if (World.Side == EnumAppSide.Server && reason == EnumDespawnReason.Death && Alive)
            {
                TrySpawnExtraLoot();
            }
            base.Die(reason, damageSourceForDeath);
        }

        private void TrySpawnExtraLoot()
        {
            int extraSets = (int)Math.Round((PlayerCount - 1) * LootSetsPerExtraPlayer);
            if (extraSets <= 0) return;

            try
            {
                var drops = Properties?.Drops;
                if (drops == null) return;

                for (int s = 0; s < extraSets; s++)
                {
                    foreach (var d in drops)
                    {
                        ItemStack stack = d?.GetNextItemStack();
                        if (stack == null || stack.StackSize <= 0) continue;
                        World.SpawnItemEntity(stack, Pos.XYZ.AddCopy(0, 0.75, 0));
                    }
                }
            }
            catch (Exception e)
            {
                World.Logger.Warning("[BotaniaStory] Gaia extra loot failed: {0}", e);
            }
        }

        // Основной тик

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (World.Side == EnumAppSide.Server)
            {
                Pos.Motion.X = 0;
                Pos.Motion.Z = 0;
                Controls.Forward = false;
                Controls.Backward = false;
                Controls.Left = false;
                Controls.Right = false;

                // Жёсткая граница
                ConstrainToArena();

                // Удержание игроков (бэкстоп) и мобов + проверка сброса ритуала
                try
                {
                    ConfinePlayersBackstop();
                    ConfineMinions();
                    CheckRitualEnd(dt);
                }
                catch (Exception e)
                {
                    if (!confineErrorLogged)
                    {
                        confineErrorLogged = true;
                        World.Logger.Warning("[BotaniaStory] Gaia confinement failed, disabled for this fight: {0}", e);
                    }
                }

                if (!Alive) return;


                // ФАЗА РОЖДЕНИЯ

                float birthTimer =
                    WatchedAttributes.GetFloat(
                        "gaiaBirthTimer",
                        0f
                    );

                if (birthTimer > 0f)
                {
                    // Во время рождения Гайа не падает, даже если арена уже уничтожена
                    Controls.IsFlying = true;

                    Pos.Motion.X = 0;
                    Pos.Motion.Y = 0;
                    Pos.Motion.Z = 0;

                    SuppressActiveAiTasks();

                    WatchedAttributes.SetFloat(
                        "gaiaBirthTimer",
                        Math.Max(0f, birthTimer - dt)
                    );

                    return;
                }


               
                // Страховка разрушенной арены
                // Выполняется ТОЛЬКО после завершения рождения
                // Весь поиск поверхности находится в AiTaskGaiaTeleport
               

                bool emergencyFloating = false;

                try
                {
                    emergencyFloating =
                        EnsureArenaSupport();
                }
                catch (Exception e)
                {
                    if (!arenaSupportErrorLogged)
                    {
                        arenaSupportErrorLogged = true;

                        World.Logger.Warning(
                            "[BotaniaStory] Gaia arena support check failed: {0}",
                            e
                        );
                    }

                    // При ошибке не позволяем Гайе упасть
                    Controls.IsFlying = true;

                    Pos.Motion.X = 0;
                    Pos.Motion.Y = 0;
                    Pos.Motion.Z = 0;

                    emergencyFloating = true;
                }



                // Проверка режимма ярости
                UpdateRageMode(dt);


               
                // ФАЗА ЛЕВИТАЦИИ
               

                if (WatchedAttributes.GetBool("isLevitating", false))
                {
                    Pos.Motion.Y = 0;
                    Controls.IsFlying = true; // ОТКЛЮЧАЕМ ГРАВИТАЦИЮ ДВИЖКА

                    // ТАЙМЕР БЕЗОПАСНОСТИ 1: Ждем пока все мобы заспавнятся
                    float graceTimer = WatchedAttributes.GetFloat("levitationGraceTimer", 0f);
                    if (graceTimer > 0)
                    {
                        WatchedAttributes.SetFloat("levitationGraceTimer", graceTimer - dt);
                        return; // Прерываем выполнение, ждем
                    }

                    // ТАЙМЕР БЕЗОПАСНОСТИ 2: Защита от софтлока (вечного зависания)
                    float maxTimer = WatchedAttributes.GetFloat("maxLevitationTimer", 60f);
                    if (maxTimer > 0)
                    {
                        WatchedAttributes.SetFloat("maxLevitationTimer", maxTimer - dt);
                    }
                    else
                    {
                        // Если время истекло, принудительно спускаем босса
                        WatchedAttributes.SetBool("isLevitating", false);
                        Controls.IsFlying = false;
                        return;
                    }

                    // Проверка прислужников раз в секунду
                    minionScanTimer += dt;
                    if (minionScanTimer > 1.0f)
                    {
                        minionScanTimer = 0f;

                        Entity[] minions = World.GetEntitiesAround(Pos.XYZ, 40f, 40f,
                            e => e.Alive && e.WatchedAttributes.GetLong("spawnedByGaia", 0) == this.EntityId);

                        // Если прислужников не осталось - Гайа падает вниз до истечения таймера
                        if (minions == null || minions.Length == 0)
                        {
                            WatchedAttributes.SetBool("isLevitating", false);
                            Controls.IsFlying = false; // ВКЛЮЧАЕМ ГРАВИТАЦИЮ ОБРАТНО
                        }
                    }

                    return; // Гайа заморожена в воздухе
                }
                else
                {
                    if (emergencyFloating)
                    {
                        Controls.IsFlying = true;
                        Pos.Motion.Y = 0;
                    }
                    else
                    {
                        Controls.IsFlying = false;
                    }
                }

                // ОБЫЧНАЯ ФАЗА (поворот к игроку)
                IPlayer nearestPlayer = World.NearestPlayer(Pos.X, Pos.Y, Pos.Z);
                if (nearestPlayer?.Entity != null)
                {
                    double dx = nearestPlayer.Entity.Pos.X - Pos.X;
                    double dz = nearestPlayer.Entity.Pos.Z - Pos.Z;
                    float targetYaw = (float)Math.Atan2(dx, dz);
                    Pos.Yaw = targetYaw;
                }
            }
            else if (World.Side == EnumAppSide.Client)
            {
                if (bossMusic != null)
                {
                    // Стартуем один раз
                    if (!isMusicStarted && Pos.X != 0)
                    {
                        bossMusic.Start();
                        isMusicStarted = true;
                    }

                    // Ручное управление затуханием (имитация 3D-звука)
                    ICoreClientAPI capi = (ICoreClientAPI)Api; // Получаем доступ к клиенту
                    if (capi.World.Player?.Entity != null)
                    {
                        float dist = (float)Pos.DistanceTo(capi.World.Player.Entity.Pos);
                        float maxDist = 60f; // Радиус арены + запас

                        if (dist > maxDist)
                        {
                            bossMusic.SetVolume(0f); // Полная тишина за ареной
                        }
                        else
                        {
                            bossMusic.SetVolume(1f - (dist / maxDist)); // Плавное затухание
                        }
                    }
                }
            }
        }

        // В фазе рождения глушим все активные AI-таски каждый тик: таск стартует внутри base.OnGameTick и тут же останавливается, не успевая нанести урон.
        private void SuppressActiveAiTasks()
        {
            try
            {
                var tm = GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
                var active = tm?.ActiveTasksBySlot;
                if (active == null) return;

                for (int i = 0; i < active.Length; i++)
                {
                    var task = active[i];
                    if (task != null) tm.StopTask(task.GetType());
                }
            }
            catch (Exception e)
            {
                if (!aiSuppressErrorLogged)
                {
                    aiSuppressErrorLogged = true;
                    World.Logger.Warning("[BotaniaStory] Gaia birth AI suppression failed: {0}", e);
                }
            }
        }

        // Горизонтальный барьер: если Гайа дальше ArenaRadius от центра спавна, подтягиваем ровно на границу круга
        private void ConstrainToArena()
        {
            double spawnX = WatchedAttributes.GetDouble("gaiaSpawnPosX", Pos.X);
            double spawnZ = WatchedAttributes.GetDouble("gaiaSpawnPosZ", Pos.Z);

            double dx = Pos.X - spawnX;
            double dz = Pos.Z - spawnZ;
            double distSq = dx * dx + dz * dz;

            if (distSq > ArenaRadius * ArenaRadius)
            {
                double dist = Math.Sqrt(distSq);
                double k = ArenaRadius / dist;

                Pos.X = spawnX + dx * k;
                Pos.Z = spawnZ + dz * k;
                Pos.Motion.X = 0;
                Pos.Motion.Z = 0;
            }
        }

        // Редкий серверный бэкстоп: работает только в полосе, умерших/респавнутых далеко не меняется
        private void ConfinePlayersBackstop()
        {
            double cx = WatchedAttributes.GetDouble("gaiaSpawnPosX", Pos.X);
            double cy = WatchedAttributes.GetDouble("gaiaSpawnPosY", Pos.Y);
            double cz = WatchedAttributes.GetDouble("gaiaSpawnPosZ", Pos.Z);

            IPlayer[] players = World.GetPlayersAround(new Vec3d(cx, cy, cz), ArenaRadius + PlayerConfineMargin + 2f, 30f);
            if (players == null) return;

            double triggerDist = ArenaRadius + PlayerHardClampBuffer;
            double outerDist = ArenaRadius + PlayerConfineMargin;

            foreach (IPlayer plr in players)
            {
                EntityPlayer pe = plr.Entity;
                if (pe == null || !pe.Alive) continue;

                EnumGameMode mode = plr.WorldData?.CurrentGameMode ?? EnumGameMode.Survival;
                if (mode == EnumGameMode.Creative || mode == EnumGameMode.Spectator) continue;

                double dx = pe.Pos.X - cx;
                double dz = pe.Pos.Z - cz;
                double distSq = dx * dx + dz * dz;

                if (distSq <= triggerDist * triggerDist) continue;
                if (distSq > outerDist * outerDist) continue;

                double dist = Math.Sqrt(distSq);
                double k = (ArenaRadius - PlayerClampReturnDepth) / dist;

                pe.TeleportToDouble(cx + dx * k, pe.Pos.Y, cz + dz * k);
            }
        }

        // Мобы, заспавненные Гайей, не могут покинуть арену
        private void ConfineMinions()
        {
            double cx = WatchedAttributes.GetDouble("gaiaSpawnPosX", Pos.X);
            double cy = WatchedAttributes.GetDouble("gaiaSpawnPosY", Pos.Y);
            double cz = WatchedAttributes.GetDouble("gaiaSpawnPosZ", Pos.Z);

            Entity[] minions = World.GetEntitiesAround(new Vec3d(cx, cy, cz), ArenaRadius + 8f, 40f,
                e => e.Alive && e.EntityId != this.EntityId
                     && e.WatchedAttributes.GetLong("spawnedByGaia", 0) == this.EntityId);
            if (minions == null) return;

            double rSq = ArenaRadius * ArenaRadius;
            foreach (Entity m in minions)
            {
                double dx = m.Pos.X - cx;
                double dz = m.Pos.Z - cz;
                double distSq = dx * dx + dz * dz;
                if (distSq <= rSq) continue;

                double dist = Math.Sqrt(distSq);
                double k = ArenaRadius / dist;
                double nx = cx + dx * k;
                double nz = cz + dz * k;

                m.Pos.X = nx;
                m.Pos.Z = nz;
                m.Pos.Motion.X = 0;
                m.Pos.Motion.Z = 0;
            }
        }

        // Ритуал сброшен, если в арене нет ни одного живого игрока дольше RitualAbandonSeconds
        // Обходим AllOnlinePlayers (надежнее GetPlayersAround). В 1.22 Pos - единственная актуальная позиция
        private void CheckRitualEnd(float dt)
        {
            double cx = WatchedAttributes.GetDouble("gaiaSpawnPosX", Pos.X);
            double cz = WatchedAttributes.GetDouble("gaiaSpawnPosZ", Pos.Z);

            double reachSq = (ArenaRadius + 3f) * (ArenaRadius + 3f);

            bool anyAlive = false;
            IPlayer[] all = World.AllOnlinePlayers;
            if (all != null)
            {
                foreach (IPlayer p in all)
                {
                    EntityPlayer pe = p.Entity;
                    if (pe == null || !pe.Alive) continue;

                    double dx = pe.Pos.X - cx;
                    double dz = pe.Pos.Z - cz;
                    if (dx * dx + dz * dz <= reachSq) { anyAlive = true; break; }
                }
            }

            if (anyAlive)
            {
                noPlayerTimer = 0f;
                return;
            }

            noPlayerTimer += dt;
            if (noPlayerTimer >= RitualAbandonSeconds)
            {
                EndRitual();
            }
        }

        // Сброс: убираем прислужников и саму Гайю без лута (это не смерть, а откат боя)
        private void EndRitual()
        {
            Entity[] minions = World.GetEntitiesAround(Pos.XYZ, ArenaRadius + 10f, 40f,
                e => e.Alive && e.EntityId != this.EntityId
                     && e.WatchedAttributes.GetLong("spawnedByGaia", 0) == this.EntityId);
            if (minions != null)
            {
                foreach (Entity m in minions) m.Die(EnumDespawnReason.Removed);
            }

            this.Die(EnumDespawnReason.Removed);
        }
        private bool EnsureArenaSupport()
        {
            // Специальная фаза левитации сама управляет положением и гравитацией Гайи
            if (WatchedAttributes.GetBool(
                "isLevitating",
                false))
            {
                return false;
            }


            // Всё нормально: под Гайей есть поверхность практически прямо под ногами
            if (AiTaskGaiaTeleport.HasImmediateArenaSupport(this))
            {
                WatchedAttributes.SetBool(
                    "gaiaEmergencyFloating",
                    false
                );

                return false;
            }



            // опоры нет Сразу блокируем падение ещё до поиска

            Pos.Motion.Y = 0;
            Controls.IsFlying = true;


           
            // Просим систему телепорта найти ближайшую поверхность на уровне арены

            if (AiTaskGaiaTeleport.TryEmergencyTeleportToNearestSupport(
                this,
                out Vec3d safePos))
            {
                Pos.SetPos(
                    safePos.X,
                    safePos.Y,
                    safePos.Z
                );

                Pos.Motion.X = 0;
                Pos.Motion.Y = 0;
                Pos.Motion.Z = 0;

                Controls.IsFlying = false;

                WatchedAttributes.SetBool(
                    "gaiaEmergencyFloating",
                    false
                );

                return false;
            }


           
            // Вообще ни одного подходящего блока на арене нет - просто зависаем.
           

            Pos.Motion.X = 0;
            Pos.Motion.Y = 0;
            Pos.Motion.Z = 0;

            Controls.IsFlying = true;

            WatchedAttributes.SetBool(
                "gaiaEmergencyFloating",
                true
            );

            // На арене буквально некуда телепортироваться
            WatchedAttributes.SetBool(
                "gaiaTeleportBlocked",
                true
            );

            return true;
        }
        private void UpdateRageMode(float dt)
        {
            if (WatchedAttributes.GetFloat(
                "gaiaBirthTimer",
                0f) > 0f)
            {
                return;
            }


            rageArenaScanTimer += dt;

            if (rageArenaScanTimer <
                RageArenaScanInterval)
            {
                return;
            }

            rageArenaScanTimer = 0f;


            try
            {
                bool wasRaging =
                    WatchedAttributes.GetBool(
                        "gaiaRageMode",
                        false
                    );



                // идеальная площать арен
                int idealColumns =
                    AiTaskGaiaTeleport.CountIdealArenaColumns(
                        this
                    );

                if (idealColumns <= 0)
                    return;



                // Сколько колонн опоры осталось


                int supportColumns =
                    AiTaskGaiaTeleport.CountArenaSupportColumns(
                        this
                    );



                // Процент разрушенности арены 


                float intactPercent =
                    (float)supportColumns /
                    idealColumns;

                float destroyedPercent =
                    1f - intactPercent;


                // На всякий случай ограничиваем 0..1
                destroyedPercent =
                    Math.Max(
                        0f,
                        Math.Min(
                            1f,
                            destroyedPercent
                        )
                    );


               
                bool shouldRage;

                if (wasRaging)
                {
                  
                    shouldRage =
                        destroyedPercent >=
                        RageExitDestroyedPercent;
                }
                else
                {
                  
                    shouldRage =
                        destroyedPercent >=
                        RageEnterDestroyedPercent;
                }


                if (shouldRage == wasRaging)
                    return;


                WatchedAttributes.SetBool(
                    "gaiaRageMode",
                    shouldRage
                );



                // Вход в режим ярости
                if (shouldRage)
                {
                    World.PlaySoundAt(
                        new AssetLocation(
                            "botaniastory",
                            "sounds/gaia_scream"
                        ),
                        Pos.X,
                        Pos.Y,
                        Pos.Z
                    );


                }
                else
                {
                }
            }
            catch (Exception e)
            {
                if (!rageScanErrorLogged)
                {
                    rageScanErrorLogged = true;

                   
                }
            }
        }
        private void InitializeRageModeOnSpawn()
        {
            try
            {
                int idealColumns =
                    AiTaskGaiaTeleport.CountIdealArenaColumns(
                        this
                    );

                if (idealColumns <= 0)
                    return;


                int supportColumns =
                    AiTaskGaiaTeleport.CountArenaSupportColumns(
                        this
                    );


                float intactPercent =
                    (float)supportColumns /
                    idealColumns;

                float destroyedPercent =
                    1f - intactPercent;


                destroyedPercent =
                    Math.Max(
                        0f,
                        Math.Min(
                            1f,
                            destroyedPercent
                        )
                    );


                bool shouldRage =
                    destroyedPercent >=
                    RageEnterDestroyedPercent;


                WatchedAttributes.SetBool(
                    "gaiaRageMode",
                    shouldRage
                );


                if (shouldRage)
                {
                    World.PlaySoundAt(
                        new AssetLocation(
                            "botaniastory",
                            "sounds/gaia_scream"
                        ),
                        Pos.X,
                        Pos.Y,
                        Pos.Z
                    );
                }
            }
            catch (Exception e)
            {
                if (!rageScanErrorLogged)
                {
                    rageScanErrorLogged = true;
                }
            }
        }
    }
}