import type { components } from "@/types/api";

type OrderStatus = components["schemas"]["OrderStatus"];
type CouponType = components["schemas"]["CouponType"];

export const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
	0: "Beklemede",
	1: "Ödendi",
	2: "Hazırlanıyor",
	3: "Kargoya Verildi",
	4: "Teslim Edildi",
	5: "İptal Edildi",
	6: "İade Edildi",
};

export const ORDER_STATUS_TRANSITIONS: Record<OrderStatus, OrderStatus[]> = {
	0: [1, 5], // Pending → Paid, Cancelled
	1: [2, 5, 6], // Paid → Preparing, Cancelled, Refunded
	2: [3, 5], // Preparing → Shipped, Cancelled
	3: [4], // Shipped → Delivered
	4: [], // Delivered — terminal
	5: [], // Cancelled — terminal
	6: [], // Refunded — terminal
};

export const COUPON_TYPE_LABELS: Record<CouponType, string> = {
	0: "Yüzde",
	1: "Sabit Tutar",
};
