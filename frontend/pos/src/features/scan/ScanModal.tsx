import { loadBarcodeReader } from '@oism/shared';
import { Alert, Button, Flex, Modal, Switch, theme } from 'antd';
import { type ChangeEvent, useEffect, useRef, useState } from 'react';

type Props = {
  open: boolean;
  onClose: () => void;
  afterClose: () => void;
  // Mã đọc được đi đúng đường của máy quét: docs/design/ui/pos.md mục "Quy tắc".
  onCode: (code: string) => void;
  // W5-15: bật thì mã được gửi sang màn hình bán hàng trên máy tính của cùng tài khoản thay vì vào giỏ ở máy này.
  remote: boolean;
  onRemoteChange: (remote: boolean) => void;
};

function cameraError(error: unknown): string {
  const name = error instanceof DOMException ? error.name : '';
  if (name === 'NotAllowedError') return 'Trình duyệt chưa được cấp quyền dùng camera.';
  if (name === 'NotFoundError' || name === 'OverconstrainedError') return 'Thiết bị không có camera.';
  return 'Không mở được camera.';
}

// Hộp quét mã vạch bằng camera hoặc ảnh, cho quầy không có máy quét (UC-POS-01 AC-8).
export function ScanModal({ open, onClose, afterClose, onCode, remote, onRemoteChange }: Props) {
  return (
    <Modal
      title="Quét mã vạch"
      open={open}
      onCancel={onClose}
      afterClose={afterClose}
      // Hộp đang ẩn không dựng lại nội dung, nên phải hủy hẳn để camera tắt và lần mở sau bắt đầu lại từ đầu.
      destroyOnHidden
      footer={<Button onClick={onClose}>Đóng</Button>}
    >
      <Flex align="center" gap={8} style={{ marginBottom: 12 }}>
        <Switch id="scan-remote" checked={remote} onChange={onRemoteChange} />
        <label htmlFor="scan-remote">Gửi sang máy tính (cùng tài khoản)</label>
      </Flex>
      <Scanner onCode={onCode} />
    </Modal>
  );
}

function Scanner({ onCode }: { onCode: (code: string) => void }) {
  const { token } = theme.useToken();
  const video = useRef<HTMLVideoElement>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const [error, setError] = useState<string>();
  const [notice, setNotice] = useState<string>();

  // Màn hình bán hàng dựng lại onCode mỗi lần render; camera không được khởi động lại theo.
  const latestOnCode = useRef(onCode);
  latestOnCode.current = onCode;

  useEffect(() => {
    const element = video.current;
    if (!element) return;
    // Trình duyệt chỉ cho mở camera trên HTTPS hoặc localhost.
    if (!navigator.mediaDevices?.getUserMedia) {
      setError('Camera chỉ mở được khi POS chạy trên HTTPS hoặc localhost.');
      return;
    }
    const stop = new AbortController();
    let stream: MediaStream | undefined;
    const release = () => stream?.getTracks().forEach((track) => track.stop());
    (async () => {
      stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
      // Hộp đã đóng trong lúc chờ cấp quyền.
      if (stop.signal.aborted) return release();
      element.srcObject = stream;
      await element.play();
      const { scanVideo } = await loadBarcodeReader();
      await scanVideo(element, (code) => latestOnCode.current(code), stop.signal);
    })().catch((reason: unknown) => {
      release();
      if (!stop.signal.aborted) setError(cameraError(reason));
    });
    return () => {
      stop.abort();
      release();
    };
  }, []);

  async function readFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    // Cho chọn lại đúng ảnh vừa chọn.
    event.target.value = '';
    if (!file) return;
    setNotice(undefined);
    try {
      const { readImage } = await loadBarcodeReader();
      const code = await readImage(file);
      if (code) latestOnCode.current(code);
      else setNotice('Không tìm thấy mã vạch trong ảnh.');
    } catch {
      setNotice('Không đọc được ảnh này.');
    }
  }

  return (
    <Flex vertical gap={12}>
      {error ? (
        <Alert type="warning" showIcon title={error} description="Vẫn quét được bằng ảnh: bấm Chọn ảnh." />
      ) : (
        <video
          ref={video}
          muted
          playsInline
          aria-label="Khung camera"
          style={{ width: '100%', aspectRatio: '4 / 3', objectFit: 'cover', background: '#000', borderRadius: token.borderRadiusLG }}
        />
      )}
      {notice && <Alert type="info" showIcon title={notice} />}
      <input ref={fileInput} type="file" accept="image/*" hidden onChange={(event) => void readFile(event)} />
      <Button block onClick={() => fileInput.current?.click()}>
        Chọn ảnh
      </Button>
    </Flex>
  );
}
