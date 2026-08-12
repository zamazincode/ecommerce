import z from "zod";

export function validate<T>(
	schema: z.ZodType<T>,
	handler: (req: Request, body: T) => Promise<Response>,
) {
	return async (req: Request) => {
		try {
			const rawBody = await req.json();

			const result = schema.safeParse(rawBody);

			if (!result.success) {
				return Response.json(
					{
						error: "Validation failed",
						details: z.treeifyError(result.error),
					},
					{ status: 400 },
				);
			}

			return handler(req, result.data);
		} catch {
			return Response.json(
				{ error: "Invalid JSON body" },
				{ status: 400 },
			);
		}
	};
}
