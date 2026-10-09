# Quản lý hồ sơ sinh viên bằng cây AVL

Ứng dụng desktop (WPF, .NET 8) quản lý hồ sơ sinh viên. Dữ liệu lưu trong file Excel, còn mọi thao tác tìm, thêm, xóa, lọc theo khoảng đều chạy trên **cây nhị phân tìm kiếm tự cân bằng AVL** trong bộ nhớ.

## 1. Chạy ứng dụng

| Cách | Lệnh |
|---|---|
| Chạy khi đang phát triển | `dotnet run --project src/AVLStudentManagement.App` |
| Chạy test | `dotnet test src/AVLStudentManagement.Tests` |
| Đóng gói thành `.exe` gửi người khác | `dotnet publish src/AVLStudentManagement.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ../Publish` |

Gửi cho người khác: nén file `AVLStudentManagement.App.exe` cùng thư mục `data` (có `HoSoSinhVien.xlsx`), đặt `data` cạnh file `.exe`. Máy nhận không cần cài .NET hay Excel.

**Vị trí file dữ liệu:** `data\HoSoSinhVien.xlsx` trong thư mục project `AVLStudentManagement.App` khi chạy từ source, hoặc `data\` cạnh file `.exe` khi đã đóng gói.

## 2. Kiến trúc

```
src/
├─ AVLStudentManagement.Core/        thư viện logic, không phụ thuộc WPF
│  ├─ DataStructures/AvlTree.cs      cây AVL generic: AvlTree<TKey, TValue>
│  ├─ Models/                        Student, Enums (Gender, Status, Grade), Catalog (khoa -> lớp)
│  ├─ Validation/StudentValidator.cs kiểm tra từng trường, trả lỗi theo tên trường
│  ├─ Data/ExcelStudentRepository.cs đọc/ghi .xlsx bằng ClosedXML
│  └─ Services/StudentService.cs     giữ 2 cây AVL + 2 HashSet, điều phối mọi thao tác
├─ AVLStudentManagement.App/         giao diện WPF, MVVM (CommunityToolkit.Mvvm)
└─ AVLStudentManagement.Tests/       MSTest (124 test)
```

Luồng một thao tác ghi (thêm, sửa, xóa, nhập):

```
Giao diện -> ViewModel -> StudentService:
  1. Chuẩn hóa và kiểm tra hợp lệ (Validator)
  2. Kiểm tra trùng khóa (cây AVL + HashSet)
  3. Cập nhật cây AVL
  4. Lưu xuống Excel  (lỗi ghi file -> hoàn tác cây ở bước 3, báo lỗi)
```

Thao tác đọc (tìm, lọc theo khoảng, thủ khoa, Top N) lấy thẳng từ cây, không đọc lại Excel.

## 3. Cấu trúc dữ liệu: hai cây AVL và hai tập băm

| Cấu trúc | Khóa | Dùng cho |
|---|---|---|
| `byId`: `AvlTree<string, Student>` | `StudentId` (so sánh ordinal) | Tìm, thêm, sửa, xóa, khoảng mã SV, danh sách có thứ tự, vẽ cây |
| `byGpa`: `AvlTree<(double Gpa, string StudentId), Student>` | cặp (điểm TB, mã SV) | Thủ khoa, điểm thấp nhất, Top N, khoảng điểm |
| `nationalIds`: `HashSet<string>` | CCCD | Kiểm tra trùng CCCD, O(1) |
| `emails`: `HashSet<string>` | Email (không phân biệt hoa thường) | Kiểm tra trùng email, O(1) |

**Vì sao cây điểm dùng khóa là cặp (điểm, mã SV):** nhiều sinh viên có thể cùng điểm, mà cây AVL ở đây không cho khóa trùng. Thêm mã SV vào khóa làm mỗi khóa duy nhất, và các sinh viên cùng điểm được xếp theo mã SV.

**Vì sao CCCD và email dùng `HashSet`, không dùng AVL:** hai trường này chỉ cần biết "đã có chưa", không cần sắp xếp hay tìm theo khoảng, nên băm O(1) là đủ.

Mỗi thao tác ghi cập nhật cả hai cây và cả hai tập băm.

## 4. Thuật toán AVL (`AvlTree.cs`)

Ký hiệu: n là số sinh viên, h là chiều cao cây (h ≤ 1,44·log₂(n+2)), k là số kết quả trả về.

### 4.1 Nền tảng: chiều cao và hệ số cân bằng
- Mỗi nút lưu sẵn `Height` (nút lá = 1, nút rỗng = 0). Chiều cao cây `Height` là O(1), không phải duyệt lại.
- `BalanceFactor = cao(trái) - cao(phải)`. Cây AVL luôn giữ hệ số này trong khoảng -1..1 tại **mọi** nút.

### 4.2 Cân bằng lại: `Rebalance`, `RotateLeft`, `RotateRight`
Sau mỗi lần thêm hoặc xóa, các nút trên đường đi từ chỗ thay đổi về gốc được cập nhật chiều cao và kiểm tra hệ số cân bằng. Nếu lệch quá 1 thì xoay:

| Ca | Điều kiện | Cách xử lý |
|---|---|---|
| Trái-Trái (LL) | lệch trái, con trái lệch trái hoặc cân bằng | xoay phải 1 lần |
| Phải-Phải (RR) | lệch phải, con phải lệch phải hoặc cân bằng | xoay trái 1 lần |
| Trái-Phải (LR) | lệch trái, con trái lệch phải | xoay trái con trái, rồi xoay phải |
| Phải-Trái (RL) | lệch phải, con phải lệch trái | xoay phải con phải, rồi xoay trái |

Mỗi phép xoay là O(1) và được đếm trong `RotationCount` (hiện ở thanh trạng thái: "Số lần xoay").

### 4.3 Các thao tác của cây

| Thao tác | Thuật toán | Độ phức tạp |
|---|---|---|
| `Insert` | Đệ quy xuống theo so sánh khóa, chèn lá, rồi `Rebalance` trên đường quay về. Khóa trùng thì trả về `false` | O(log n) |
| `Delete` | Tìm nút. 0 hoặc 1 con: lấy con lên thay. 2 con: lấy nút nhỏ nhất của cây con phải (nút kế tiếp) thay vào rồi xóa nút đó. Rồi `Rebalance` trên đường quay về | O(log n) |
| `TryGet` / `TryUpdate` | Đi từ gốc, nhỏ hơn thì sang trái, lớn hơn thì sang phải (vòng lặp, không đệ quy). `TryUpdate` chỉ đổi giá trị, không đổi cấu trúc | O(log n) |
| `Min` / `Max` | Đi hết về bên trái / bên phải | O(log n) |
| `Range(from, to)` | Duyệt giữa bằng stack, **cắt nhánh**: bỏ qua cả cây con có khóa nhỏ hơn `from`, dừng hẳn khi gặp khóa lớn hơn `to` | O(log n + k) |
| `InOrder` / `InOrderDescending` | Duyệt giữa bằng stack (trái-gốc-phải, hoặc phải-gốc-trái) | O(n) |
| `PreOrder` | Gốc-trái-phải bằng stack | O(n) |
| `PostOrder` | Hai stack: lấy thứ tự gốc-phải-trái rồi đảo ngược | O(n) |

Mọi thao tác duyệt dùng stack tường minh, không đệ quy, nên không bị tràn bộ nhớ ngăn xếp. `Insert` và `Delete` đệ quy nhưng độ sâu chỉ là h ≈ log n.

## 5. Chức năng và thuật toán AVL được dùng

| Chức năng | Cách hoạt động trên cây | Độ phức tạp |
|---|---|---|
| **Mở app, nạp dữ liệu** | Đọc Excel, `Insert` từng sinh viên vào cả hai cây và hai tập băm. Có trùng khóa trong file thì báo lỗi kèm số dòng | O(n log n) |
| **Danh sách sinh viên** | `InOrder()` trên `byId`: luôn theo mã SV tăng dần, không cần sắp xếp lại | O(n) |
| **Tìm theo mã SV** | `TryGet` trên `byId` | O(log n) |
| **Thêm sinh viên** | Chuẩn hóa, kiểm tra hợp lệ, kiểm tra trùng mã SV (`TryGet`), CCCD, email (`HashSet`), rồi `Insert` vào hai cây, lưu Excel. Lưu lỗi thì `Delete` ngược lại | O(log n) + ghi file |
| **Sửa sinh viên** | `Find` bản cũ, kiểm tra hợp lệ và trùng, `TryUpdate` trên `byId` (mã SV không đổi nên cấu trúc không đổi). Cây điểm: `Delete` khóa cũ rồi `Insert` khóa mới vì điểm có thể đổi. Lưu lỗi thì hoàn tác | O(log n) + ghi file |
| **Xóa sinh viên** | `Delete` khỏi hai cây và hai tập băm (có xoay cân bằng nếu cần). Lưu lỗi thì `Insert` lại | O(log n) + ghi file |
| **Tìm theo khoảng mã SV** | `Range(từ, đến)` trên `byId` | O(log n + k) |
| **Xóa theo khoảng mã SV** | `Range` lấy danh sách các sinh viên trong khoảng, rồi `Delete` từng người, lưu một lần. Lưu lỗi thì chèn lại cả nhóm | O(log n + k log n) |
| **Tìm theo khoảng điểm TB** | `Range((min, ""), (max, "￿"))` trên `byGpa`. Cận dưới/trên của mã SV bao hết mọi mã ứng với điểm đó | O(log n + k) |
| **Thủ khoa** | `Max()` trên `byGpa` | O(log n) |
| **Điểm thấp nhất** | `Min()` trên `byGpa` | O(log n) |
| **Top N** | `InOrderDescending()` trên `byGpa` lấy N phần tử đầu, dừng sớm (không duyệt hết cây) | O(log n + N) |
| **Lọc nhiều tiêu chí** (tên, lớp, khoa, trạng thái, xếp loại) | LINQ `Where` trên `InOrder()`. Tìm chuỗi con thì cây nào cũng phải xét từng phần tử, nên **không dùng được tính chất cây** | O(n) |
| **Sắp xếp theo cột khác** | Dùng sẵn của DataGrid (bấm tiêu đề cột). Bấm F5 trở lại thứ tự cây | O(n log n) |
| **Nhập từ Excel** | Với mỗi sinh viên trong file: chuẩn hóa, kiểm tra hợp lệ, kiểm tra trùng, `Insert`. Chỉ lưu Excel một lần. Báo rõ từng sinh viên bị bỏ qua và lý do. Lưu lỗi thì hoàn tác toàn bộ | O(m log n) |
| **Xuất ra Excel** | Chép file dữ liệu hiện tại (luôn mới nhất vì mỗi thao tác đã lưu) | O(kích thước file) |
| **Trực quan hóa cây** (Ctrl+2) | Duyệt giữa: x là thứ tự in-order, y là độ sâu; hiện h và hệ số cân bằng bf của từng nút, đổi giữa Pre/In/Post-order | O(n) |
| **Thanh trạng thái** | Tổng SV (`Count`), chiều cao h (`Height`), log₂(n), số lần xoay (`RotationCount`), thời gian thao tác gần nhất | O(1) |

## 6. Kiểm tra dữ liệu (Validator)

| Trường | Quy tắc |
|---|---|
| Mã SV | 8-12 chữ số, duy nhất, không sửa được sau khi tạo |
| Họ tên | 2-100 ký tự, tự bỏ khoảng trắng thừa |
| Ngày sinh | tuổi từ 15 đến 60 |
| Giới tính | Nam, Nữ hoặc Khác |
| CCCD | đúng 12 chữ số, duy nhất |
| Email | đúng định dạng, duy nhất (không phân biệt hoa thường) |
| Số điện thoại | tùy chọn; nếu có thì 10 số, bắt đầu bằng 0 |
| Địa chỉ | tối đa 255 ký tự |
| Khoa, Lớp | phải có trong danh mục, và lớp phải thuộc đúng khoa |
| Trạng thái | Đang học, Bảo lưu, Đã tốt nghiệp, Thôi học |
| Điểm TB | từ 0 đến 4, làm tròn 2 chữ số |
| Xếp loại | không lưu, tính từ điểm: Xuất sắc ≥ 3,6; Giỏi ≥ 3,2; Khá ≥ 2,5; Trung bình ≥ 2,0; Yếu ≥ 1,0; Kém < 1,0 |

Lỗi hiện ngay cạnh ô nhập tương ứng.

## 7. Cơ sở dữ liệu Excel

File `HoSoSinhVien.xlsx` có hai sheet:
- `SinhVien`: dòng 1 là tiêu đề (đọc theo tên cột nên đổi thứ tự cột vẫn đúng), mỗi dòng sau là một sinh viên.
- `DanhMuc`: hai cột Khoa, Lop, dùng để đổ vào ô chọn và để kiểm tra.

**Ghi an toàn:** ghi ra file `.tmp`, rồi thay vào file chính và giữ một bản `.bak`. Nếu app tắt giữa chừng thì file chính không bị hỏng. Nếu file đang mở trong Excel làm ghi lỗi, thao tác được hoàn tác và app báo lỗi.

**Giới hạn:** mỗi thao tác ghi lại toàn bộ file (đo thực tế: 5.000 sinh viên ghi khoảng 1,3 giây). Phù hợp quy mô lớp, khoa. Nếu lên hàng chục nghìn thì nên chuyển sang SQLite.

## 8. Phím tắt

| Phím | Chức năng |
|---|---|
| Ctrl+N | Làm mới form để thêm mới |
| Ctrl+S | Lưu (thêm hoặc cập nhật) |
| Ctrl+D hoặc Delete | Xóa dòng đang chọn |
| F5 | Hiện tất cả |
| Ctrl+F hoặc F3 | Đến ô tìm nhanh |
| Ctrl+I / Ctrl+E | Nhập / Xuất Excel |
| Esc | Bỏ chọn, xóa form, đóng phần nâng cao |
| Ctrl+1 / Ctrl+2 | Tab Hồ sơ / Cấu trúc cây |
| F4 | Ẩn/hiện panel chi tiết |
| Ctrl+Shift+F | Bộ lọc nâng cao |
| F1 | Bảng phím tắt |
| Ctrl+Q | Thoát |

## 9. Kiểm thử và hiệu năng

124 test, chia theo nhóm:
- **Cây AVL (28):** bốn ca xoay LL, RR, LR, RL; xóa lá, nút một con, nút hai con, gốc, cây một nút; `Range`, `Min`/`Max`, các kiểu duyệt; 10.000 thao tác ngẫu nhiên đối chiếu với `SortedDictionary`; chèn 100.000 khóa tăng dần mà chiều cao vẫn cỡ logarit.
- **Validator (45):** từng trường, gồm các giá trị sát biên.
- **Service (36):** trùng khóa, đồng bộ hai chỉ mục, hoàn tác khi lưu lỗi, nhập hàng loạt.
- **Excel (9) và file thực (1):** đọc/ghi, file hỏng, báo lỗi đúng dòng.
- **End-to-end (5):** thêm/sửa/xóa/nhập trên file Excel thật rồi mở lại; đo tốc độ.

Số đo trên 100.000 sinh viên (máy phát triển):

| Thao tác | Kết quả |
|---|---|
| Chiều cao cây | h = 20 (giới hạn AVL ≈ 25) |
| Tìm theo mã | ≈ 0,8 µs/lần |
| Tìm khoảng mã (50 kết quả) | ≈ 2 ms |
| Tìm khoảng điểm (344 kết quả) | ≈ 2,4 ms |
| Top 10 | ≈ 2,7 ms |
| Nạp và dựng cây | ≈ 640 ms |

Các số khoảng và Top 10 chỉ đo một lần nên gồm cả thời gian khởi động của .NET.