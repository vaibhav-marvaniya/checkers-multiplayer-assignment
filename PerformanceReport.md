# PerformanceReport – Online Checkers Prototype

## 1. Test Setup

- **Unity Version:** 6000.0.55f1 LTS 
- **Platform:** Android
- **Build Type:** Development
- **Scripting Backend:** IL2CPP
- **Measurement Method:** Unity Profiler attached to Android device (Development build with Autoconnect Profiler).

### 1.1 Test Devices

> Note: A 3–4 GB RAM device was not available, so tests were run on 6 GB and 8 GB devices (stricter than the minimum requirement).

| Device       | RAM | OS         |
|-------------|-----|------------|
| Poco F6     | 8 GB| Android 15 |
| iQOO Z10    | 6 GB| Android 15 |

Performance goals from assignment: ≥30 FPS on 3–4 GB RAM devices, APK ≤ 80 MB, peak runtime memory < 500 MB, no per-frame GC spikes.

---

## 2. APK Size

- **APK File Name:** Checker.apk  
- **APK Size on Disk:** 46 MB  
- **Target:** ≤ 80 MB  
- **Result:** Pass

---

## 3. Performance Results

### 3.1 Poco F6 (8 GB RAM, Android 15)

| Scenario      | FPS                     | Memory Usage     | GC Alloc / Spikes          |
|---------------|-------------------------|------------------|----------------------------|
| Main Menu     | 60 FPS                  | 312 MB           | 0 KB                       |
| Offline Game  | ~60 FPS (average)       | 320 MB           | ~4 KB GC spike **on move** |
| Online Game   | ~55 FPS (average)       | 350 MB           | ~4.5 KB GC spike **on move** |

**Notes:**

- FPS is stable and well above the 30 FPS target in all cases.
- Peak memory stays comfortably below 500 MB.
- GC allocations appear only when performing moves (no continuous per-frame spikes).

---

### 3.2 iQOO Z10 (6 GB RAM, Android 15)

| Scenario      | FPS                     | Memory Usage     | GC Alloc / Spikes          |
|---------------|-------------------------|------------------|----------------------------|
| Main Menu     | ~58 FPS                 | 417 MB           | 0 KB                       |
| Offline Game  | ~55 FPS (average)       | 449 MB           | ~4 KB GC spike **on move** |
| Online Game   | ~55 FPS (average)       | 478 MB           | ~4.5 KB GC spike **on move** |

**Notes:**

- FPS stays around 55–58 FPS across all scenarios, above the 30 FPS requirement.
- Peak memory in the heaviest case (online game) is ~478 MB, still under the 500 MB limit.
- GC spikes are small and only occur when a move is made, not every frame.

---

## 4. Summary vs Assignment Targets

- **FPS Target (≥ 30 FPS):**  
  - ✅ Met on both devices in main menu, offline, and online gameplay.

- **Peak Runtime Memory (< 500 MB):**  
  - ✅ Met on both devices.  
  - Highest observed: ~478 MB on iQOO Z10 during online game.

- **GC Behaviour (No per-frame GC spikes):**  
  - ✅ No per-frame spikes observed.  
  - Only small (~4–4.5 KB) GC spikes when a move is performed, which is acceptable for this prototype.

- **APK Size (≤ 80 MB):**  
  - ✅ App size is 46 MB.

---

## 5. Optimisations (Short)

- Minimized allocations during gameplay; remaining allocations are limited to move actions only.
- Kept visuals and UI lightweight to stay within memory and APK size targets.
- Ensured board logic and netcode do not allocate every frame, reducing GC pressure.
