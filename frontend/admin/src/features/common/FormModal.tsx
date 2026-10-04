import { ApiError, errorMessage } from '@oism/shared';
import { useMutation } from '@tanstack/react-query';
import { Alert, Form, Modal } from 'antd';
import type { ReactNode } from 'react';

type Props<Values> = {
  title: string;
  initialValues?: Partial<Values>;
  // Gọi API tạo hoặc sửa. Xong thì hộp thoại đóng.
  submit: (values: Values) => Promise<unknown>;
  onClose: () => void;
  width?: number;
  // Các Form.Item của bản ghi.
  children: ReactNode;
};

// Tên trường của server sang đường dẫn trường của form: "skus[0].SkuCode" thành ['skus', 0, 'skuCode'].
const toFieldPath = (key: string) =>
  key
    .split(/[.[\]]+/)
    .filter(Boolean)
    .map((part) => (/^\d+$/.test(part) ? Number(part) : part.charAt(0).toLowerCase() + part.slice(1)));

// Hộp thoại tạo hoặc sửa một bản ghi. Màn hình chỉ dựng nó khi cần mở, nên mỗi lần mở là một form mới.
// Lỗi `validation_failed` hiện cạnh từng trường; lỗi khác hiện ở đầu form (docs/conventions/frontend.md).
export function FormModal<Values extends object>({ title, initialValues, submit, onClose, width, children }: Props<Values>) {
  const [form] = Form.useForm<Values>();
  const { mutate, isPending, error } = useMutation({
    mutationFn: submit,
    onSuccess: onClose,
    onError: (failure) => {
      if (failure instanceof ApiError && failure.code === 'validation_failed') {
        form.setFields(Object.entries(failure.errors).map(([name, errors]) => ({ name: toFieldPath(name) as never, errors })));
      }
    },
  });

  return (
    <Modal
      open
      title={title}
      okText="Lưu"
      cancelText="Hủy"
      width={width}
      confirmLoading={isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
    >
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Form<Values> form={form} layout="vertical" initialValues={initialValues} onFinish={(values) => !isPending && mutate(values)}>
        {children}
        {/* Nút lưu nằm ở chân hộp thoại, ngoài form: nút ẩn này để nhấn Enter trong ô nhập cũng lưu. */}
        <button type="submit" hidden />
      </Form>
    </Modal>
  );
}
