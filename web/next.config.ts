import type { NextConfig } from "next";

const nextConfig: NextConfig = {
	images: {
		remotePatterns: [
			{ protocol: "https", hostname: "i.dr.com.tr" },
			{ protocol: "https", hostname: "res.cloudinary.com" },
		],
	},
};

export default nextConfig;
