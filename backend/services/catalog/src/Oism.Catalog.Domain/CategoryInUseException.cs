using Oism.SharedKernel;

namespace Oism.Catalog.Domain;

// UC-PROD-01 AC-3. Bảng mã lỗi: docs/conventions/backend.md mục "Lỗi".
public sealed class CategoryInUseException()
    : OismException("category_in_use", "Danh mục còn danh mục con hoặc sản phẩm");
