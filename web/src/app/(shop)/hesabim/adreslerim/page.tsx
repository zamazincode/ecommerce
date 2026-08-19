"use client";

import { MapPinIcon } from "lucide-react";
import { useAddresses, useDeleteAddress } from "@/hooks/use-addresses";
import { AddressFormDialog } from "@/components/account/address-form-dialog";
import { PageHeader } from "@/components/admin/page-header";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { EmptyState } from "@/components/ui/empty-state";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";

export default function AddressesPage() {
	const { data: addresses, isLoading } = useAddresses();
	const deleteAddress = useDeleteAddress();

	return (
		<div>
			<PageHeader
				title="Adreslerim"
				className="mb-6"
				actions={
					<AddressFormDialog trigger={<Button>Yeni Adres</Button>} />
				}
			/>

			{isLoading ? null : addresses?.length === 0 ? (
				<EmptyState
					icon={MapPinIcon}
					title="Henüz kayıtlı adresin yok"
					description="Sipariş verebilmek için en az bir adres eklemelisin."
					action={
						<AddressFormDialog
							trigger={<Button>Adres Ekle</Button>}
						/>
					}
				/>
			) : (
				<div className="grid gap-4 sm:grid-cols-2">
					{addresses?.map((address) => (
						<Card key={address.id} className="p-4">
							<div className="mb-2 flex items-center justify-between">
								<p className="flex items-center gap-1.5 font-medium">
									<MapPinIcon className="size-4 text-muted-foreground" />
									{address.title}
								</p>
								{address.isDefault ? (
									<Badge variant="brand-soft">
										Varsayılan
									</Badge>
								) : null}
							</div>
							<p className="text-sm text-muted-foreground">
								{address.fullName} · {address.phone}
							</p>
							<p className="text-sm text-muted-foreground">
								{address.fullAddress}, {address.district}/
								{address.city}
							</p>
							<div className="mt-3 flex gap-2">
								<AddressFormDialog
									address={address}
									trigger={
										<Button variant="outline" size="sm">
											Düzenle
										</Button>
									}
								/>
								<ConfirmDialog
									trigger={
										<Button
											variant="destructive"
											size="sm"
										>
											Sil
										</Button>
									}
									title={`"${address.title}" adresi silinsin mi?`}
									description="Bu işlem geri alınamaz."
									confirmLabel="Sil"
									tone="danger"
									onConfirm={() =>
										deleteAddress.mutate(
											address.id as number,
										)
									}
								/>
							</div>
						</Card>
					))}
				</div>
			)}
		</div>
	);
}
