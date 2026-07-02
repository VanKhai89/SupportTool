# Kiến trúc Class và Cấu trúc thư mục (Memory Viewer)

Bản kế hoạch này mô tả chi tiết cách sắp xếp mã nguồn, đảm bảo tuân thủ nguyên tắc chia nhỏ (Single Responsibility), tránh tạo ra một file quá lớn khó kiểm soát (God Object). Ứng dụng sẽ đi theo mô hình MVVM (Model-View-ViewModel) cơ bản của WPF.

## User Review Required

Bạn hãy xem qua cách chia class dưới đây. Nếu cấu trúc này hợp lý, dễ đọc và dễ bảo trì theo ý bạn, chúng ta sẽ bắt đầu tạo từng phần một.

## Proposed Changes

Dưới đây là danh sách các class sẽ được tạo ra, chia theo thư mục (Layer):

### 1. Thư mục `Core` (Tương tác hệ thống cấp thấp)
Chỉ chịu trách nhiệm giao tiếp với hệ điều hành Windows, không chứa logic UI.

#### [NEW] `Core/NativeMethods.cs`
- Chỉ chứa các khai báo `[DllImport("kernel32.dll")]`.
- Các hàm: `OpenProcess`, `ReadProcessMemory`, `WriteProcessMemory`, `CloseHandle`.

#### [NEW] `Core/ProcessManager.cs`
- Hàm: `GetProcessList()` (lọc bỏ các process hệ thống không cần thiết).
- Hàm: `AttachProcess(int pId)`. Nắm giữ `IntPtr processHandle` để dùng chung cho toàn bộ app.

#### [NEW] `Core/MemoryReader.cs`
- Class bọc (Wrapper) thao tác trực tiếp với RAM.
- Các hàm tiện ích: `ReadBytes(addr, length)`, `ReadInt32(addr)`, `ReadFloat(addr)`, `WriteString(addr, val)`...
- Đóng gói việc xử lý con trỏ (Pointer path): `ResolvePointer(baseAddress, offsets)`.

---

### 2. Thư mục `Models` (Cấu trúc dữ liệu)
Đại diện cho dữ liệu thuần túy hiển thị trên bộ nhớ.

#### [NEW] `Models/MemoryItem.cs`
- Kế thừa `INotifyPropertyChanged` (Để WPF tự động update giao diện khi giá trị thay đổi).
- Đại diện cho 1 DÒNG trên màn hình Grid.
- **Properties**: 
  - `OffsetString` (VD: `+0x04`)
  - `Address` (địa chỉ thật)
  - `HexValue`, `Int32Value`, `FloatValue` (giá trị current).
  - `SnapshotInt32`, `SnapshotFloat` (giá trị mốc).
  - `Label` (Ghi chú).
  - `IsFrozen` và `LockValue`.
- Cung cấp hàm `UpdateValue(...)` để tính toán chênh lệch (Delta) so với Snapshot.

#### [NEW] `Models/PointerOffset.cs`
- Dùng cho cửa sổ Add Address để biểu diễn 1 cấu trúc con trỏ.
- **Properties**: `BaseAddress`, `List<int> Offsets`.

---

### 3. Thư mục `Utils` (Tiện ích hỗ trợ)
Xử lý các thao tác chuỗi, logic toán học, không phụ thuộc vào hệ thống hay UI.

#### [NEW] `Utils/HexConverter.cs`
- Chuyên xử lý định dạng Hex.
- Hàm: `ParseHexToInt(string)` (Biến "0x1A" thành 26).
- Hàm: `IntToHexString(int)` (Biến 26 thành "0x0000001A").

#### [NEW] `Utils/DeltaScanner.cs`
- Sinh ra danh sách các địa chỉ cần đọc.
- Nhận vào `BaseAddress`, khoảng `DeltaFrom` (-100), `DeltaTo` (+200), `Stride` (khoảng cách nhảy byte). Trả về danh sách các `Address` cần tạo.

---

### 4. Thư mục `ViewModels` (Logic xử lý Giao diện)
Cầu nối giữa Data và UI, quản lý các Event click và Vòng lặp Real-time.

#### [NEW] `ViewModels/MainViewModel.cs`
- Nắm giữ danh sách `ObservableCollection<MemoryItem> MemoryList`.
- Chứa logic của **DispatcherTimer (100ms)**: Mỗi nhịp tick sẽ lấy `MemoryList` đẩy vào `MemoryReader` để đọc và cập nhật các `.Int32Value`, `.FloatValue` mới.
- Chứa logic Filter (Quét danh sách và ẩn đi các dòng không thỏa mãn Range Filter).
- Chứa logic của nút Snapshot (Lấy Current Value đập vào Snapshot Value).

#### [NEW] `ViewModels/AddAddressViewModel.cs`
- Xử lý riêng cho cửa sổ Add Address. 
- Xử lý sự kiện khi gõ TextBox Offset thì lập tức gọi `MemoryReader` để preview kết quả hiển thị ra nhãn "-> 12345".

---

### 5. Thư mục `Views` (Giao diện XAML)

#### [NEW] `Views/MainWindow.xaml`
- XAML định nghĩa Grid, các TextBox.
- Sử dụng DataTrigger để binding: Trả về màu **Xanh/Đỏ** tự động dựa vào chênh lệch giữa `Int32Value` và `SnapshotInt32` của `MemoryItem`.

#### [NEW] `Views/AddAddressWindow.xaml`
- XAML cho cửa sổ Add Address.

---

## Verification Plan
Việc chia nhỏ như trên đảm bảo mỗi class rất mỏng (từ 50 đến tối đa 300 dòng code). Bạn dễ dàng quản lý việc "Ai chịu trách nhiệm đọc RAM" (MemoryReader), "Ai chịu trách nhiệm tính toán Hex" (HexConverter) hay "Ai lo update màu mè giao diện" (MainViewModel & MemoryItem).
